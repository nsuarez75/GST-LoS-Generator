using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GstLosGenerator.Core;
using GstLosGenerator.Core.Tia;

namespace GstLosGenerator.App.ViewModels;

public enum TiaPickMode
{
    // Leer: hay que elegir un bloque.
    Block,
    // Subir: un PLC o una carpeta donde crear el FB si aún no existe; si ya existe, se sustituye donde esté.
    Destination,
}

// Ventana para elegir en TIA el proyecto, el PLC y el bloque (o la carpeta de destino).
public sealed partial class TiaPickerViewModel : ObservableObject
{
    // Proyectos abiertos en un TIA del usuario a los que ya se ha avisado de que la app se conecta; solo mientras
    // la app sigue abierta.
    private static readonly HashSet<string> Warned = new(StringComparer.OrdinalIgnoreCase);

    private readonly IDialogs _dialogs;
    private readonly ITiaSession _session;
    private readonly TiaLink? _remembered;
    private readonly Progress<string> _progress;
    private IReadOnlyList<string> _open = [];
    private TiaTree? _tree;

    public string Title { get; }
    public TiaPickMode Mode { get; }
    public ObservableCollection<string> Projects { get; } = [];
    public ObservableCollection<TiaNode> Nodes { get; } = [];

    [ObservableProperty]
    public partial string? Project { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanAccept))]
    public partial TiaNode? Selected { get; set; }

    [ObservableProperty]
    public partial string Status { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanAccept))]
    public partial bool IsBusy { get; set; }

    // Lo elegido al aceptar; null si se cancela.
    public TiaLink? Result { get; private set; }

    // Árbol del proyecto elegido, para quien abrió la ventana (por ejemplo, para saber si el FB ya existe).
    public TiaTree? Tree => _tree;

    // La ventana se cierra cuando se acepta.
    public event Action? Done;

    public TiaPickerViewModel(IDialogs dialogs, ITiaSession session, TiaPickMode mode, TiaLink? remembered, string title)
    {
        _dialogs = dialogs;
        _session = session;
        _remembered = remembered;
        Mode = mode;
        Title = title;
        _progress = new Progress<string>(text => Status = text);
    }

    public string Hint => Mode == TiaPickMode.Block
        ? L.T("Elige el bloque.")
        : L.T("Elige el PLC o la carpeta. Si el FB ya existe en ese PLC, se sustituye donde está.");

    public bool CanAccept => !IsBusy && Selected is { } node && (Mode == TiaPickMode.Destination || node.Kind == TiaNodeKind.Block);

    // Al abrir la ventana: los proyectos abiertos en TIA y el recordado.
    public async Task Load()
    {
        IsBusy = true;
        Status = L.T("Buscando proyectos abiertos en TIA…");
        try
        {
            _open = await _session.OpenProjects();
        }
        catch (TiaException e)
        {
            _open = [];
            Status = e.Message;
        }
        finally
        {
            IsBusy = false;
        }
        foreach (var path in _open.Prepend(_remembered?.Project).OfType<string>().Where(p => p.Length > 0))
        {
            AddProject(path);
        }
        if (Projects.Count == 0)
        {
            Status = L.T("No hay ningún proyecto abierto en TIA. Abre uno con «Abrir proyecto…».");
            return;
        }
        Project = Projects[0];
    }

    private void AddProject(string path)
    {
        if (!Projects.Contains(path, StringComparer.OrdinalIgnoreCase))
        {
            Projects.Add(path);
        }
    }

    partial void OnProjectChanged(string? value)
    {
        if (value is not null)
        {
            _ = LoadTree(value);
        }
    }

    public async Task LoadTree(string project)
    {
        Nodes.Clear();
        Selected = null;
        _tree = null;
        if (_open.Contains(project, StringComparer.OrdinalIgnoreCase) && !Warned.Contains(project))
        {
            if (!await _dialogs.Confirm(L.T("Conectar con TIA Portal"),
                    L.T("La app se va a conectar con el TIA Portal donde tienes abierto {0}. Guarda antes lo que tengas sin guardar. La primera vez, TIA te pedirá permiso.", Path.GetFileName(project)),
                    L.T("Conectar")))
            {
                Status = L.T("Sin conectar.");
                return;
            }
            Warned.Add(project);
        }
        IsBusy = true;
        try
        {
            var tree = await _session.Tree(project, _progress);
            if (Project != project)
            {
                return;
            }
            _tree = tree;
            Build(tree);
            Status = tree.Plcs.Count == 0 ? L.T("El proyecto no tiene ningún PLC.") : "";
        }
        catch (TiaException e)
        {
            Status = e.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void Build(TiaTree tree)
    {
        foreach (var plc in tree.Plcs)
        {
            var plcNode = new TiaNode(TiaNodeKind.Plc, plc, "", plc, "");
            var groups = new Dictionary<string, TiaNode> { [""] = plcNode };
            foreach (var group in tree.Groups.Where(g => g.Plc == plc))
            {
                var slash = group.Path.LastIndexOf('/');
                var parent = groups.GetValueOrDefault(slash < 0 ? "" : group.Path[..slash]) ?? plcNode;
                var node = new TiaNode(TiaNodeKind.Group, plc, group.Path, group.Path[(slash + 1)..], "") { IsExpanded = false };
                parent.Children.Add(node);
                groups[group.Path] = node;
            }
            foreach (var block in tree.Blocks.Where(b => b.Plc == plc))
            {
                var parent = groups.GetValueOrDefault(block.Group) ?? plcNode;
                parent.Children.Add(new TiaNode(TiaNodeKind.Block, plc, block.Group, block.Name, block.Kind));
            }
            Nodes.Add(plcNode);
        }
        Selected = Preselect();
        if (Selected is not null)
        {
            ExpandTo(Selected);
        }
    }

    // El bloque recordado o, al subir, el FB si ya existe.
    private TiaNode? Preselect()
    {
        var all = Nodes.SelectMany(Flatten).ToList();
        if (_remembered is { } link && string.Equals(link.Project, Project, StringComparison.OrdinalIgnoreCase))
        {
            var match = all.FirstOrDefault(n => n.Kind == TiaNodeKind.Block && n.Plc == link.Plc && n.Group == link.Group && n.Name == link.Block)
                ?? all.FirstOrDefault(n => n.Kind == TiaNodeKind.Block && n.Plc == link.Plc && n.Name == link.Block)
                ?? (Mode == TiaPickMode.Destination ? all.FirstOrDefault(n => n.Kind == TiaNodeKind.Group && n.Plc == link.Plc && n.Group == link.Group) : null);
            if (match is not null)
            {
                return match;
            }
        }
        return Mode == TiaPickMode.Destination && Nodes.Count == 1
            ? all.FirstOrDefault(n => n.Kind == TiaNodeKind.Block && n.Name == Naming.FbName) ?? Nodes[0]
            : null;
    }

    private static IEnumerable<TiaNode> Flatten(TiaNode node) => node.Children.SelectMany(Flatten).Prepend(node);

    private void ExpandTo(TiaNode target)
    {
        foreach (var root in Nodes)
        {
            Expand(root, target);
        }
    }

    private static bool Expand(TiaNode node, TiaNode target)
    {
        if (node == target)
        {
            return true;
        }
        foreach (var child in node.Children)
        {
            if (Expand(child, target))
            {
                node.IsExpanded = true;
                return true;
            }
        }
        return false;
    }

    // Al subir: dónde acabará el FB con lo elegido.
    public string DestinationNote
    {
        get
        {
            if (Mode != TiaPickMode.Destination || Selected is not { } node || _tree is null)
            {
                return "";
            }
            return _tree.FindBlock(node.Plc, Naming.FbName) is { } existing
                ? L.T("{0} ya existe en {1}: se sustituye ahí.", Naming.FbName, Place(existing.Plc, existing.Group))
                : L.T("{0} no existe en {1}: se crea en {2}.", Naming.FbName, node.Plc, Place(node.Plc, node.Group));
        }
    }

    private static string Place(string plc, string group) => group.Length == 0 ? plc : plc + " / " + group;

    partial void OnSelectedChanged(TiaNode? value) => OnPropertyChanged(nameof(DestinationNote));

    [RelayCommand]
    private async Task OpenFile()
    {
        var path = await _dialogs.OpenFile(L.T("Abrir proyecto de TIA"), new FileFilter("TIA Portal V19", ".ap19"));
        if (path is not null)
        {
            AddProject(path);
            Project = Projects.First(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        }
    }

    [RelayCommand]
    private async Task Reload()
    {
        if (Project is { } project)
        {
            await LoadTree(project);
        }
    }

    [RelayCommand]
    public void Accept()
    {
        if (!CanAccept || Selected is not { } node || Project is not { } project)
        {
            return;
        }
        Result = new TiaLink
        {
            Project = project,
            Plc = node.Plc,
            Group = node.Group,
            Block = node.Kind == TiaNodeKind.Block ? node.Name : "",
        };
        Done?.Invoke();
    }
}

public enum TiaNodeKind
{
    Plc,
    Group,
    Block,
}

// Nodo del árbol de TIA: PLC, carpeta de bloques o bloque.
public sealed partial class TiaNode(TiaNodeKind kind, string plc, string group, string name, string detail) : ObservableObject
{
    public TiaNodeKind Kind { get; } = kind;
    public string Plc { get; } = plc;
    // Carpeta del nodo (la suya, si es una carpeta; donde está, si es un bloque).
    public string Group { get; } = group;
    public string Name { get; } = name;
    // Tipo de bloque (FB, FC, GlobalDB...).
    public string Detail { get; } = detail;
    public ObservableCollection<TiaNode> Children { get; } = [];

    [ObservableProperty]
    public partial bool IsExpanded { get; set; } = true;

    public string Icon => Kind switch
    {
        TiaNodeKind.Plc => "cpu",
        TiaNodeKind.Group => "folder",
        _ => "file-code",
    };
}
