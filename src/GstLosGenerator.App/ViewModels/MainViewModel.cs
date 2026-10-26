using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GstLosGenerator.Core;
using GstLosGenerator.Core.Fb;

namespace GstLosGenerator.App.ViewModels;

public sealed record IssueViewModel(Issue Issue)
{
    public string Text => Issue.Plc.Length > 0 ? $"{Issue.Plc} · {Issue.Text}" : Issue.Text;
}

// Ventana principal: menú, árbol del proyecto, editor central y barra de avisos.
public sealed partial class MainViewModel : ObservableObject
{
    public static readonly FileFilter ProjectFilter = new("GST LoS Generator", Storage.ProjectExtension);
    public static readonly FileFilter CatalogFilter = new(L.T("Catálogo de tipos"), Storage.CatalogExtension);
    public static readonly FileFilter LosFilter = new("Excel", ".xlsx");
    public static readonly FileFilter XmlFilter = new("XML", ".xml");

    private readonly Action<AppSettings> _saveSettings;

    public Session Session { get; }
    // La ventana sobre la que se abren los diálogos; cambia si se vuelve a crear la ventana.
    public IDialogs Dialogs { get; set; }
    public AppSettings Settings { get; }
    public ObservableCollection<NavNode> Tree { get; } = [];
    public ObservableCollection<IssueViewModel> Issues { get; } = [];

    // Cambio de tema o idioma; lo aplica la aplicación.
    public event Action? AppearanceChanged;

    [ObservableProperty]
    public partial NavNode? SelectedNode { get; set; }

    [ObservableProperty]
    public partial object? Editor { get; set; }

    [ObservableProperty]
    public partial string Title { get; set; } = "";

    [ObservableProperty]
    public partial string IssuesSummary { get; set; } = "";

    [ObservableProperty]
    public partial IssueViewModel? SelectedIssue { get; set; }

    public MainViewModel(Session session, IDialogs dialogs, AppSettings settings, Action<AppSettings> saveSettings)
    {
        Session = session;
        Dialogs = dialogs;
        Settings = settings;
        _saveSettings = saveSettings;
        Session.Changed += OnChanged;
        Session.Reset += Rebuild;
        Rebuild();
    }

    public bool IsThemeSystem => Settings.Theme == AppSettings.ThemeSystem;
    public bool IsThemeLight => Settings.Theme == AppSettings.ThemeLight;
    public bool IsThemeDark => Settings.Theme == AppSettings.ThemeDark;
    public bool IsSpanish => L.Language == L.Spanish;
    public bool IsEnglish => L.Language == L.English;

    // ---------------------------------------------------------------- árbol y editor

    public void Rebuild()
    {
        var kind = SelectedNode?.Kind ?? NavKind.Project;
        var plcName = SelectedNode?.Plc?.Name;
        Tree.Clear();
        var root = new NavNode(NavKind.Project, null);
        foreach (var plc in Session.Project.Plcs)
        {
            var node = new NavNode(NavKind.Plc, plc) { IsExpanded = Session.Project.Plcs.Count <= 4 };
            node.Children.Add(new NavNode(NavKind.Devices, plc));
            node.Children.Add(new NavNode(NavKind.Assets, plc));
            node.Children.Add(new NavNode(NavKind.Operations, plc));
            node.Children.Add(new NavNode(NavKind.Locations, plc));
            root.Children.Add(node);
        }
        Tree.Add(root);
        RefreshTitles();
        SelectedNode = FindNode(kind, plcName) ?? root;
        OnChanged();
    }

    private void RefreshTitles()
    {
        foreach (var root in Tree)
        {
            root.Title = Session.Project.Name.Length > 0 ? Session.Project.Name : L.T("Proyecto");
            root.Detail = L.T("{0} PLC", Session.Project.Plcs.Count);
            foreach (var node in root.Children)
            {
                var plc = node.Plc!;
                node.Title = plc.Name;
                node.Detail = plc.CurrentFbFile is { } fb ? Path.GetFileName(fb) : "";
                foreach (var section in node.Children)
                {
                    (section.Title, section.Detail) = section.Kind switch
                    {
                        NavKind.Devices => (L.T("Devices"), plc.Devices.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                        NavKind.Assets => (L.T("Assets"), plc.Assets.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                        NavKind.Operations => (L.T("Operaciones"), plc.Operations.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                        _ => (L.T("Locations"), plc.Locations.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                    };
                }
            }
        }
    }

    public NavNode? FindNode(NavKind kind, string? plc)
    {
        foreach (var root in Tree)
        {
            if (root.Matches(kind, plc))
            {
                return root;
            }
            foreach (var node in root.Children)
            {
                if (node.Matches(kind, plc))
                {
                    return node;
                }
                var section = node.Children.FirstOrDefault(s => s.Matches(kind, plc));
                if (section is not null)
                {
                    node.IsExpanded = true;
                    return section;
                }
            }
        }
        return null;
    }

    partial void OnSelectedNodeChanged(NavNode? value)
    {
        Editor = value is null ? null : value.Kind switch
        {
            NavKind.Project => new ProjectEditorViewModel(this),
            NavKind.Plc => new PlcEditorViewModel(this, value.Plc!),
            NavKind.Devices => new DevicesViewModel(Session, value.Plc!),
            NavKind.Assets => new AssetsViewModel(Session, value.Plc!, Dialogs),
            NavKind.Operations => new OperationsViewModel(Session, Dialogs, value.Plc!),
            _ => new LocationsViewModel(Session, value.Plc!),
        };
    }

    private void OnChanged()
    {
        var name = Session.Project.Name.Length > 0 ? Session.Project.Name : L.T("Proyecto");
        Title = $"GST LoS Generator — {name}{(Session.Dirty ? " *" : "")}";
        RefreshTitles();
        Issues.Clear();
        foreach (var issue in Validation.Check(Session.Project, Session.Catalog))
        {
            Issues.Add(new IssueViewModel(issue));
        }
        IssuesSummary = Issues.Count switch
        {
            0 => L.T("Sin avisos"),
            1 => L.T("1 aviso"),
            _ => L.T("{0} avisos", Issues.Count),
        };
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    // Ir a lo que provoca un aviso.
    partial void OnSelectedIssueChanged(IssueViewModel? value)
    {
        if (value is null)
        {
            return;
        }
        var issue = value.Issue;
        var kind = issue.Target switch
        {
            IssueTarget.Operation => NavKind.Operations,
            IssueTarget.Device => NavKind.Devices,
            _ => NavKind.Assets,
        };
        SelectedNode = FindNode(kind, issue.Plc) ?? SelectedNode;
        if (Editor is ISelectable selectable)
        {
            selectable.Select(issue.Subject);
        }
    }

    // ---------------------------------------------------------------- archivo

    [RelayCommand]
    private async Task New()
    {
        if (await ConfirmDiscard())
        {
            Session.New();
        }
    }

    [RelayCommand]
    private async Task Open()
    {
        if (!await ConfirmDiscard())
        {
            return;
        }
        var path = await Dialogs.OpenFile(L.T("Abrir proyecto"), ProjectFilter);
        if (path is not null)
        {
            await OpenPath(path);
        }
    }

    public async Task OpenPath(string path)
    {
        try
        {
            Session.Open(path);
            Settings.LastProject = path;
            _saveSettings(Settings);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            await Dialogs.Message(L.T("Abrir proyecto"), L.T("No se pudo abrir {0}: {1}", Path.GetFileName(path), e.Message));
        }
    }

    [RelayCommand]
    public async Task<bool> Save() => Session.ProjectPath is { } path ? await SaveTo(path) : await SaveAs();

    [RelayCommand]
    private async Task<bool> SaveAs()
    {
        var name = (Session.Project.Name.Length > 0 ? Session.Project.Name : L.T("Proyecto")) + Storage.ProjectExtension;
        var path = await Dialogs.SaveFile(L.T("Guardar proyecto"), ProjectFilter, name);
        return path is not null && await SaveTo(path);
    }

    private async Task<bool> SaveTo(string path)
    {
        try
        {
            Session.Save(path);
            Settings.LastProject = path;
            _saveSettings(Settings);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            await Dialogs.Message(L.T("Guardar proyecto"), L.T("No se pudo guardar: {0}", e.Message));
            return false;
        }
    }

    // true si se puede seguir: no hay cambios, se han guardado o se descartan.
    public async Task<bool> ConfirmDiscard()
    {
        if (!Session.Dirty)
        {
            return true;
        }
        return await Dialogs.AskSave() switch
        {
            SaveChoice.Save => await Save(),
            SaveChoice.Discard => true,
            _ => false,
        };
    }

    [RelayCommand]
    private async Task ImportLos()
    {
        var path = await Dialogs.OpenFile(L.T("Importar LoS"), LosFilter);
        if (path is null)
        {
            return;
        }
        LosData data;
        try
        {
            data = await Task.Run(() => LosReader.Read(path));
        }
        // ClosedXML lanza excepciones de varios tipos con un Excel dañado o abierto en otro programa.
        catch (Exception e)
        {
            await Dialogs.Message(L.T("Importar LoS"), L.T("No se pudo leer la LoS: {0}", e.Message));
            return;
        }
        var plan = LosImportPlan.Create(Session.Project, Session.Catalog, data);
        if (!plan.HasChanges)
        {
            await Dialogs.Message(L.T("Importar LoS"), L.T("La LoS no trae cambios respecto al proyecto."));
            return;
        }
        var hasTypeVariables = plan.Changes.Any(c => c.Kind == ChangeKind.TypeVariableAdded);
        var (accepted, option) = await Dialogs.Changes(L.T("Importar LoS: {0}", Path.GetFileName(path)),
            plan.Changes.Select(c => (c.Plc.Length > 0 ? c.Plc + " · " : "") + c.Text).ToList(), [],
            hasTypeVariables ? L.T("Añadir a cada tipo las variables nuevas que traen sus assets") : null);
        if (!accepted)
        {
            return;
        }
        plan.AddVariablesToTypes = option;
        Session.Change("import-los", () =>
        {
            plan.Apply();
            Session.Project.LosFile = Path.GetFileName(path);
            if (Session.Project.Name.Length == 0 || Session.Project.Name == L.T("Proyecto nuevo"))
            {
                Session.Project.Name = Path.GetFileNameWithoutExtension(path);
            }
        });
        Rebuild();
    }

    // ---------------------------------------------------------------- editar

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo() => Session.Undo();

    private bool CanUndo() => Session.CanUndo;

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo() => Session.Redo();

    private bool CanRedo() => Session.CanRedo;

    // ---------------------------------------------------------------- proyecto

    [RelayCommand]
    public async Task AddPlc()
    {
        var values = await Dialogs.Prompt(L.T("Añadir PLC"), [L.T("Nombre (como en la LoS)"), L.T("Número")], ["PLC01", "1"]);
        if (values is null || values[0].Trim().Length == 0)
        {
            return;
        }
        var name = values[0].Trim();
        if (Session.Project.FindPlc(name) is not null)
        {
            await Dialogs.Message(L.T("Añadir PLC"), L.T("Ya hay un PLC llamado {0}.", name));
            return;
        }
        var number = int.TryParse(values[1], out var n) ? n : Naming.PlcNumber(name);
        Session.Change("add-plc", () => Session.Project.Plcs.Add(new Plc { Name = name, Number = number }));
        Rebuild();
        SelectedNode = FindNode(NavKind.Plc, name);
    }

    public async Task RemovePlc(Plc plc)
    {
        if (!await Dialogs.Confirm(L.T("Quitar PLC"), L.T("¿Quitar {0} del proyecto con todos sus assets y operaciones?", plc.Name), L.T("Quitar")))
        {
            return;
        }
        Session.Change("remove-plc", () => Session.Project.Plcs.Remove(plc));
        SelectedNode = null;
        Rebuild();
    }

    [RelayCommand]
    private async Task OpenCatalog()
    {
        await Dialogs.ShowCatalog(new CatalogViewModel(Session, Dialogs));
        Rebuild();
    }

    [RelayCommand]
    private async Task ReadFb()
    {
        var plc = await ChoosePlc(L.T("Leer FB"));
        if (plc is not null)
        {
            await ReadFbFor(plc);
        }
    }

    public async Task ReadFbFor(Plc plc)
    {
        var path = await Dialogs.OpenFile(L.T("Leer FB de {0}", plc.Name), XmlFilter);
        if (path is null)
        {
            return;
        }
        FbSyncPlan plan;
        try
        {
            var xml = await File.ReadAllTextAsync(path);
            plan = FbSyncPlan.Create(plc, Session.Catalog, xml, path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or FbFormatException)
        {
            await Dialogs.Message(L.T("Leer FB"), L.T("No se pudo leer el FB: {0}", e.Message));
            return;
        }
        var changes = plan.Changes.Count > 0 ? plan.Changes : [L.T("La configuración ya coincide con el FB.")];
        var (accepted, _) = await Dialogs.Changes(L.T("Leer FB de {0}: {1}", plc.Name, Path.GetFileName(path)), changes, plan.Warnings);
        if (accepted)
        {
            Session.Change("read-fb", plan.Apply);
            Rebuild();
        }
    }

    [RelayCommand]
    private async Task Generate()
    {
        if (Session.Project.Plcs.Count == 0)
        {
            await Dialogs.Message(L.T("Generar"), L.T("El proyecto no tiene PLCs: importa una LoS o añade uno."));
            return;
        }
        var plc = SelectedNode?.Plc ?? Session.Project.Plcs[0];
        await GenerateFor(plc);
    }

    public async Task GenerateFor(Plc plc)
    {
        await Dialogs.ShowGenerate(new GenerateViewModel(this, plc));
        OnChanged();
    }

    // PLC del nodo seleccionado o, si hay varios, el que elija el usuario.
    private async Task<Plc?> ChoosePlc(string title)
    {
        if (SelectedNode?.Plc is { } selected)
        {
            return selected;
        }
        if (Session.Project.Plcs.Count == 1)
        {
            return Session.Project.Plcs[0];
        }
        if (Session.Project.Plcs.Count == 0)
        {
            await Dialogs.Message(title, L.T("El proyecto no tiene PLCs: importa una LoS o añade uno."));
            return null;
        }
        var names = string.Join(", ", Session.Project.Plcs.Select(p => p.Name));
        var values = await Dialogs.Prompt(title, [L.T("PLC ({0})", names)], [Session.Project.Plcs[0].Name]);
        return values is null ? null : Session.Project.FindPlc(values[0].Trim());
    }

    // ---------------------------------------------------------------- ver

    [RelayCommand]
    private void SetTheme(string theme)
    {
        Settings.Theme = theme;
        _saveSettings(Settings);
        AppearanceChanged?.Invoke();
    }

    [RelayCommand]
    private void SetLanguage(string language)
    {
        Settings.Language = language;
        L.Language = L.Normalize(language);
        _saveSettings(Settings);
        AppearanceChanged?.Invoke();
    }

    [RelayCommand]
    private Task About() => Dialogs.About(L.T("Acerca de"), L.T("Creado por Ignacio Suarez."), UpdateCheck.RepositoryUrl);

    // ---------------------------------------------------------------- versión nueva

    public static Version CurrentVersion => typeof(MainViewModel).Assembly.GetName().Version ?? new Version(0, 0, 0);

    // Al abrir: si en GitHub hay una release más nueva que esta versión, se avisa en una barra arriba.
    public async Task CheckForUpdates(Func<CancellationToken, Task<ReleaseInfo?>> latest)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var release = await latest(timeout.Token);
        if (release is not null && UpdateCheck.IsNewer(release, CurrentVersion))
        {
            UpdateUrl = new Uri(release.Url);
            UpdateNotice = L.T("Hay una versión nueva: {0} (tienes la {1}).", release.Tag, CurrentVersion.ToString(3));
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUpdate))]
    public partial string? UpdateNotice { get; set; }

    [ObservableProperty]
    public partial Uri? UpdateUrl { get; set; }

    public bool HasUpdate => UpdateNotice is not null;

    [RelayCommand]
    private void DismissUpdate() => UpdateNotice = null;
}

// Editores que saben ir a un elemento concreto (desde la barra de avisos).
public interface ISelectable
{
    void Select(string subject);
}
