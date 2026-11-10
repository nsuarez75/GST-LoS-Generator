using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GstLosGenerator.Core;
using GstLosGenerator.Core.Fb;
using GstLosGenerator.Core.Tia;

namespace GstLosGenerator.App.ViewModels;

// Catálogo de tipos, común a todos los proyectos: equivalencia de cada tipo de la LoS con su FB y UDT del PLC.
public sealed partial class CatalogViewModel : EditableViewModel
{
    private readonly IDialogs _dialogs;
    private readonly ITia _tia;

    public ObservableCollection<AssetTypeViewModel> Types { get; } = [];

    [ObservableProperty]
    public partial AssetTypeViewModel? Selected { get; set; }

    [ObservableProperty]
    public partial string Filter { get; set; } = "";

    [ObservableProperty]
    public partial bool OnlyPending { get; set; }

    public CatalogViewModel(Session session, IDialogs dialogs, ITia? tia = null) : base(session)
    {
        _dialogs = dialogs;
        _tia = tia ?? new NoTia();
        Build();
    }

    public string Summary => L.T("{0} tipos · {1} sin equivalencia", Session.Catalog.Types.Count, Session.Catalog.Types.Count(t => !t.HasEquivalence));

    partial void OnFilterChanged(string value) => Build();

    partial void OnOnlyPendingChanged(bool value) => Build();

    private void Build()
    {
        var selected = Selected?.Type.Name;
        Types.Clear();
        var filter = Filter.Trim();
        foreach (var type in Session.Catalog.Types.OrderBy(t => t.Name, StringComparer.Ordinal))
        {
            if ((filter.Length == 0 || type.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) || type.Fb.Contains(filter, StringComparison.OrdinalIgnoreCase))
                && (!OnlyPending || !type.HasEquivalence))
            {
                Types.Add(new AssetTypeViewModel(Session, this, type));
            }
        }
        Selected = Types.FirstOrDefault(t => t.Type.Name == selected) ?? Types.FirstOrDefault();
        OnPropertyChanged(nameof(Summary));
    }

    public void Refresh() => OnPropertyChanged(nameof(Summary));

    [RelayCommand]
    private async Task Add()
    {
        var values = await _dialogs.Prompt(L.T("Nuevo tipo"), [L.T("Nombre (Template de la LoS)")], [""]);
        var name = values?[0].Trim() ?? "";
        if (name.Length == 0)
        {
            return;
        }
        if (Session.Catalog.Find(name) is not null)
        {
            await _dialogs.Message(L.T("Nuevo tipo"), L.T("Ya hay un tipo {0}.", name));
            return;
        }
        Session.Change("add-type", () => Session.Catalog.Types.Add(new AssetType { Name = name }));
        Filter = "";
        Build();
        Selected = Types.FirstOrDefault(t => t.Type.Name == name);
    }

    [RelayCommand]
    private async Task Remove()
    {
        if (Selected is not { } vm)
        {
            return;
        }
        var used = Session.Project.Plcs.Sum(p => p.Assets.Count(a => a.Type == vm.Type.Name));
        var text = used > 0
            ? L.T("¿Eliminar el tipo {0}? Lo usan {1} assets del proyecto abierto.", vm.Type.Name, used)
            : L.T("¿Eliminar el tipo {0}?", vm.Type.Name);
        if (await _dialogs.Confirm(L.T("Eliminar tipo"), text, L.T("Eliminar")))
        {
            Session.Change("remove-type", () => Session.Catalog.Types.Remove(vm.Type));
            Build();
        }
    }

    [RelayCommand]
    private async Task Export()
    {
        var path = await _dialogs.SaveFile(L.T("Exportar catálogo"), MainViewModel.CatalogFilter, "catalogo" + Storage.CatalogExtension);
        if (path is null)
        {
            return;
        }
        try
        {
            Storage.SaveCatalog(Session.Catalog, path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            await _dialogs.Message(L.T("Exportar catálogo"), L.T("No se pudo guardar: {0}", e.Message));
        }
    }

    // Rellena la equivalencia y la interfaz del tipo seleccionado desde un FB de TIA o un XML exportado de TIA.
    // Solo toca ese tipo; antes de aplicar enseña las diferencias.
    [RelayCommand]
    private async Task LearnFromFb()
    {
        if (Selected is not { } vm)
        {
            return;
        }
        var title = L.T("Leer FB Tipo: {0}", vm.Type.Name);
        var fromTia = false;
        if (_tia.Available)
        {
            var choice = await _dialogs.Pick(title, L.T("¿De dónde se lee el FB?"), [L.T("Desde TIA…"), L.T("Desde archivo…")]);
            if (choice is null)
            {
                return;
            }
            fromTia = choice == 0;
        }
        string path;
        List<LearnSource> sources;
        try
        {
            string xml;
            if (fromTia)
            {
                using var session = _tia.Open();
                var picker = new TiaPickerViewModel(_dialogs, session, TiaPickMode.Block, null, title);
                await _dialogs.ShowTiaPicker(picker);
                if (picker.Result is not { } link)
                {
                    return;
                }
                xml = await session.Export(link, new Progress<string>());
                path = MainViewModel.TiaLabel(link);
            }
            else
            {
                if (await _dialogs.OpenFile(title, MainViewModel.XmlFilter) is not { } file)
                {
                    return;
                }
                path = file;
                xml = await File.ReadAllTextAsync(path);
            }
            sources = TypeLearner.Sources(xml);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or FbFormatException or TiaException)
        {
            await _dialogs.Message(title, L.T("No se pudo leer el FB: {0}", e.Message));
            return;
        }
        if (sources.Count == 0)
        {
            await _dialogs.Message(title, L.T("El XML no llama a ningún FB ni tiene una interfaz de la que aprender."));
            return;
        }
        var source = sources[0];
        if (sources.Count > 1)
        {
            var current = sources.FindIndex(s => s.Fb == vm.Type.Fb);
            var index = await _dialogs.Pick(title, L.T("El XML llama a varios FB. Elige el que corresponde a {0}:", vm.Type.Name),
                sources.Select(s => s.Label).ToList(), Math.Max(current, 0));
            if (index is not { } chosen)
            {
                return;
            }
            source = sources[chosen];
        }
        var changes = TypeLearner.Differences(vm.Type, source);
        if (changes.Count == 0)
        {
            await _dialogs.Message(title, L.T("El tipo ya coincide con el FB."));
            return;
        }
        var warnings = source.UdtPin is null ? [L.T("No se ve qué pin lleva el UDT; esa parte del tipo no se toca.")] : new List<string>();
        var (accepted, _) = await _dialogs.Changes(L.T("{0}: {1}", title, Path.GetFileName(path)), changes, warnings);
        if (!accepted)
        {
            return;
        }
        Session.Change("learn-type", () => TypeLearner.Apply(vm.Type, source));
        Selected = null;
        Build();
        Selected = Types.FirstOrDefault(t => t.Type == vm.Type) ?? Selected;
    }

    // Los tipos del archivo se añaden o sustituyen a los del mismo nombre; el resto se queda.
    [RelayCommand]
    private async Task Import()
    {
        var path = await _dialogs.OpenFile(L.T("Importar catálogo"), MainViewModel.CatalogFilter);
        if (path is null)
        {
            return;
        }
        Catalog incoming;
        try
        {
            incoming = Storage.LoadCatalog(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            await _dialogs.Message(L.T("Importar catálogo"), L.T("No se pudo leer {0}: {1}", Path.GetFileName(path), e.Message));
            return;
        }
        var changes = incoming.Types.Select(t => Session.Catalog.Find(t.Name) is null
            ? L.T("Tipo nuevo: {0}", t.Name)
            : L.T("Se sustituye: {0}", t.Name)).ToList();
        if (changes.Count == 0)
        {
            await _dialogs.Message(L.T("Importar catálogo"), L.T("El archivo no tiene tipos."));
            return;
        }
        var (accepted, _) = await _dialogs.Changes(L.T("Importar catálogo"), changes, []);
        if (!accepted)
        {
            return;
        }
        var merged = Storage.Clone(Session.Catalog);
        foreach (var type in incoming.Types)
        {
            merged.Types.RemoveAll(t => t.Name == type.Name);
            merged.Types.Add(type);
        }
        Session.ReplaceCatalog(merged);
        Build();
    }
}

public sealed partial class AssetTypeViewModel : EditableViewModel
{
    private readonly CatalogViewModel _owner;

    public AssetType Type { get; }
    public ObservableCollection<TypeVariableViewModel> Variables { get; } = [];
    public static IReadOnlyList<string> Sections { get; } = ["Output", "InOut", "Input"];

    [ObservableProperty]
    public partial TypeVariableViewModel? SelectedVariable { get; set; }

    public AssetTypeViewModel(Session session, CatalogViewModel owner, AssetType type) : base(session)
    {
        _owner = owner;
        Type = type;
        foreach (var variable in type.Variables)
        {
            Variables.Add(new TypeVariableViewModel(session, variable));
        }
    }

    public string Name => Type.Name;
    public bool IsPending => !Type.HasEquivalence;
    public string Status => Type.NoFb ? L.T("Sin FB") : Type.HasEquivalence ? $"{Type.Fb} · {Type.Udt}" : L.T("Sin equivalencia");

    public bool NoFb
    {
        get => Type.NoFb;
        set
        {
            if (Edit(Type.NoFb, value, v => Type.NoFb = v))
            {
                OnPropertyChanged(nameof(IsPending));
                OnPropertyChanged(nameof(Status));
                _owner.Refresh();
            }
        }
    }
    public string Parameters => Type.Parameters.Count == 0
        ? L.T("Se aprende con «Leer FB LoS» si el FB usa este tipo, o con «Leer FB Tipo…».")
        : string.Join("\n", Type.Parameters.Select(p => $"{p.Name}  ({p.Section})  {p.Type}"));

    public string Fb
    {
        get => Type.Fb;
        set => Set(Type.Fb, value.Trim(), v => Type.Fb = v);
    }

    public string Udt
    {
        get => Type.Udt;
        set => Set(Type.Udt, value.Trim(), v => Type.Udt = v);
    }

    public string UdtPin
    {
        get => Type.UdtPin;
        set => Set(Type.UdtPin, value.Trim(), v => Type.UdtPin = v);
    }

    public string? UdtPinSection
    {
        get => Type.UdtPinSection;
        set
        {
            if (value is not null)
            {
                Set(Type.UdtPinSection, value, v => Type.UdtPinSection = v);
            }
        }
    }

    public string OperationPin
    {
        get => Type.OperationPin;
        set => Set(Type.OperationPin, value.Trim(), v => Type.OperationPin = v);
    }

    private void Set(string current, string value, Action<string> set, [System.Runtime.CompilerServices.CallerMemberName] string? property = null)
    {
        if (Edit(current, value, set, property))
        {
            OnPropertyChanged(nameof(IsPending));
            OnPropertyChanged(nameof(Status));
            _owner.Refresh();
        }
    }

    // Propuesta habitual: FB LoS_<tipo>.
    [RelayCommand]
    private void Suggest()
    {
        if (Type.Fb.Length == 0)
        {
            Fb = "LoS_" + Type.Name;
            OnPropertyChanged(nameof(Fb));
        }
    }

    [RelayCommand]
    private void AddVariable()
    {
        var variable = new TypeVariable { Name = L.T("NuevaVariable"), DataType = "Bool" };
        Session.Change("add-type-variable", () => Type.Variables.Add(variable));
        var vm = new TypeVariableViewModel(Session, variable);
        Variables.Add(vm);
        SelectedVariable = vm;
    }

    [RelayCommand]
    private void RemoveVariable()
    {
        if (SelectedVariable is not { } vm)
        {
            return;
        }
        Session.Change("remove-type-variable", () => Type.Variables.Remove(vm.Variable));
        Variables.Remove(vm);
    }
}

public sealed partial class TypeVariableViewModel(Session session, TypeVariable variable) : EditableViewModel(session)
{
    public TypeVariable Variable { get; } = variable;
    public IReadOnlyList<string> DataTypeChoices => DeviceViewModel.DataTypeChoices;

    public string Name
    {
        get => Variable.Name;
        set => Edit(Variable.Name, value.Trim(), v => Variable.Name = v);
    }

    public string? DataType
    {
        get => Variable.DataType;
        set
        {
            if (value is not null)
            {
                Edit(Variable.DataType, value, v => Variable.DataType = v);
            }
        }
    }

    public string Source
    {
        get => Variable.Source;
        set => Edit(Variable.Source, value.Trim(), v => Variable.Source = v);
    }
}
