using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GstLosGenerator.Core;

namespace GstLosGenerator.App.ViewModels;

// Árbol de assets de un PLC (celda → asset de operación → sub-assets) con buscador.
// Cada asset se despliega y enseña sus variables con tipo de dato y dirección.
public sealed partial class AssetsViewModel : EditableViewModel, ISelectable
{
    public const string NoOperation = "—";

    private readonly IDialogs? _dialogs;

    public Plc Plc { get; }
    public ObservableCollection<AssetNodeViewModel> Roots { get; } = [];
    public IReadOnlyList<string> TypeNames { get; }
    public IReadOnlyList<string> OperationChoices { get; }

    [ObservableProperty]
    public partial string Search { get; set; } = "";

    [ObservableProperty]
    public partial object? Selected { get; set; }

    public AssetsViewModel(Session session, Plc plc, IDialogs? dialogs = null) : base(session)
    {
        Plc = plc;
        _dialogs = dialogs;
        TypeNames = Session.Catalog.Types.Select(t => t.Name).Concat(plc.Assets.Select(a => a.Type))
            .Where(n => n.Length > 0).Distinct().Order(StringComparer.Ordinal).ToList();
        OperationChoices = [NoOperation, .. plc.Operations.Select(o => o.Id).Order(Core.Fb.OperationIdComparer.Instance)];
        Build();
    }

    public string Summary => L.T("{0} assets", Plc.Assets.Count);

    internal Session EditSession => Session;

    partial void OnSearchChanged(string value) => Build();

    private void Build()
    {
        Roots.Clear();
        var tree = new AssetTree(Plc.Assets);
        var filter = Search.Trim();
        foreach (var root in tree.Roots)
        {
            var node = Create(root, tree, filter);
            if (node is not null)
            {
                Roots.Add(node);
            }
        }
    }

    // null si ni el asset ni ninguno de sus descendientes coincide con la búsqueda.
    private AssetNodeViewModel? Create(Asset asset, AssetTree tree, string filter)
    {
        var children = tree.Children(asset).Select(c => Create(c, tree, filter)).OfType<AssetNodeViewModel>().ToList();
        var matches = filter.Length == 0 || Matches(asset, filter);
        if (!matches && children.Count == 0)
        {
            return null;
        }
        var node = new AssetNodeViewModel(this, asset);
        foreach (var variable in asset.Variables)
        {
            node.Children.Add(new VariableRowViewModel(Session, node, variable));
        }
        foreach (var child in children)
        {
            node.Children.Add(child);
        }
        node.IsExpanded = filter.Length > 0 ? children.Count > 0 : asset.Kind == AssetKind.Cell;
        return node;
    }

    private static bool Matches(Asset asset, string filter) =>
        asset.Element.Contains(filter, StringComparison.OrdinalIgnoreCase)
        || asset.Type.Contains(filter, StringComparison.OrdinalIgnoreCase)
        || asset.Variables.Any(v => v.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) || v.Address.Contains(filter, StringComparison.OrdinalIgnoreCase));

    public string StatusOf(Asset asset)
    {
        var parts = new List<string>();
        if (asset.Manual)
        {
            parts.Add(L.T("manual"));
        }
        if (asset.Missing)
        {
            parts.Add(L.T("ya no está en la LoS"));
        }
        if (Session.Catalog.Find(asset.Type) is not { HasEquivalence: true })
        {
            parts.Add(L.T("sin equivalencia"));
        }
        if (!string.IsNullOrWhiteSpace(asset.FbOverride) || !string.IsNullOrWhiteSpace(asset.UdtOverride))
        {
            parts.Add(L.T("FB/UDT propios"));
        }
        return string.Join(" · ", parts);
    }

    public void Select(string subject)
    {
        Search = "";
        foreach (var root in Roots)
        {
            if (Find(root, subject) is { } node)
            {
                Selected = node;
                return;
            }
        }
    }

    private static AssetNodeViewModel? Find(AssetNodeViewModel node, string element)
    {
        if (node.Asset.Element == element)
        {
            return node;
        }
        foreach (var child in node.Children.OfType<AssetNodeViewModel>())
        {
            if (Find(child, element) is { } found)
            {
                node.IsExpanded = true;
                return found;
            }
        }
        return null;
    }

    private AssetNodeViewModel? SelectedAsset => Selected switch
    {
        AssetNodeViewModel a => a,
        VariableRowViewModel v => v.Owner,
        _ => null,
    };

    [RelayCommand]
    private async Task AddAsset()
    {
        if (_dialogs is null)
        {
            return;
        }
        var parent = SelectedAsset?.Asset.Element;
        var values = await _dialogs.Prompt(L.T("Añadir asset"),
            [L.T("Nombre (Element)"), L.T("Tipo")],
            [parent is null ? "" : parent + "_", SelectedAsset?.Asset.Type ?? ""]);
        if (values is null || values[0].Trim().Length == 0)
        {
            return;
        }
        var element = values[0].Trim();
        if (Plc.FindAsset(element) is not null)
        {
            await _dialogs.Message(L.T("Añadir asset"), L.T("Ya hay un asset llamado {0}.", element));
            return;
        }
        var asset = new Asset { Element = element, Type = values[1].Trim(), Kind = AssetKind.Asset, Manual = true };
        Session.Change("add-asset", () =>
        {
            // Detrás de su padre y sus descendientes, para respetar el orden de la LoS.
            var index = parent is null ? Plc.Assets.Count : LastDescendantIndex(parent) + 1;
            Plc.Assets.Insert(index, asset);
        });
        Build();
        Select(element);
    }

    private int LastDescendantIndex(string element)
    {
        var last = Plc.Assets.FindIndex(a => a.Element == element);
        for (var i = 0; i < Plc.Assets.Count; i++)
        {
            if (Plc.Assets[i].Element.StartsWith(element + "_", StringComparison.Ordinal))
            {
                last = Math.Max(last, i);
            }
        }
        return last;
    }

    [RelayCommand]
    private async Task RemoveAsset()
    {
        if (SelectedAsset is not { } node || _dialogs is null)
        {
            return;
        }
        if (!await _dialogs.Confirm(L.T("Eliminar asset"), L.T("¿Eliminar {0}? Sus sub-assets no se eliminan.", node.Asset.Element), L.T("Eliminar")))
        {
            return;
        }
        Session.Change("remove-asset", () => Plc.Assets.Remove(node.Asset));
        Selected = null;
        Build();
    }

    [RelayCommand]
    private void AddVariable()
    {
        if (SelectedAsset is not { } node)
        {
            return;
        }
        var variable = new AssetVariable { Name = L.T("NuevaVariable"), DataType = "Bool", Direction = "OUT" };
        Session.Change("add-variable", () => node.Asset.Variables.Add(variable));
        var row = new VariableRowViewModel(Session, node, variable);
        node.Children.Insert(node.Asset.Variables.Count - 1, row);
        node.IsExpanded = true;
        Selected = row;
    }

    [RelayCommand]
    private void RemoveVariable()
    {
        if (Selected is not VariableRowViewModel row)
        {
            return;
        }
        Session.Change("remove-variable", () => row.Owner.Asset.Variables.Remove(row.Variable));
        row.Owner.Children.Remove(row);
        Selected = row.Owner;
    }

    [RelayCommand]
    private void ExpandAll() => SetExpanded(Roots, true);

    [RelayCommand]
    private void CollapseAll() => SetExpanded(Roots, false);

    private static void SetExpanded(IEnumerable<AssetNodeViewModel> nodes, bool expanded)
    {
        foreach (var node in nodes)
        {
            node.IsExpanded = expanded;
            SetExpanded(node.Children.OfType<AssetNodeViewModel>(), expanded);
        }
    }
}

public sealed partial class AssetNodeViewModel(AssetsViewModel owner, Asset asset) : EditableViewModel(owner.EditSession)
{
    public AssetsViewModel Owner { get; } = owner;
    public Asset Asset { get; } = asset;
    public ObservableCollection<object> Children { get; } = [];

    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

    public string Element => Asset.Element;
    public IReadOnlyList<string> TypeNames => Owner.TypeNames;
    public IReadOnlyList<string> OperationChoices => Owner.OperationChoices;
    public string Status => Owner.StatusOf(Asset);
    public bool HasStatus => Status.Length > 0;

    public string KindGlyph => Asset.Kind switch
    {
        AssetKind.Cell => "▦",
        AssetKind.Operation => "◆",
        AssetKind.Asset => "◇",
        _ => "▣",
    };

    public string? Type
    {
        get => Asset.Type;
        set
        {
            if (value is not null && Edit(Asset.Type, value, v => Asset.Type = v))
            {
                OnPropertyChanged(nameof(Status));
                OnPropertyChanged(nameof(HasStatus));
            }
        }
    }

    public string? Operation
    {
        get => string.IsNullOrEmpty(Asset.Operation) ? AssetsViewModel.NoOperation : Asset.Operation;
        set
        {
            var id = value is null || value == AssetsViewModel.NoOperation ? null : value;
            Edit(Asset.Operation, id, v => Asset.Operation = v);
        }
    }
}

public sealed partial class VariableRowViewModel(Session session, AssetNodeViewModel owner, AssetVariable variable) : EditableViewModel(session)
{
    public AssetNodeViewModel Owner { get; } = owner;
    public AssetVariable Variable { get; } = variable;

    public static IReadOnlyList<string> Directions { get; } = ["OUT", "IN"];

    // Sin marcar: en Struct TO DEVICE se escribe comentada.
    public bool Enabled
    {
        get => !Variable.Disabled;
        set => Edit(!Variable.Disabled, value, v => Variable.Disabled = !v);
    }

    public string Name
    {
        get => Variable.Name;
        set => Edit(Variable.Name, value.Trim(), v => Variable.Name = v);
    }

    public string DataType
    {
        get => Variable.DataType;
        set => Edit(Variable.DataType, value.Trim(), v => Variable.DataType = v);
    }

    public string? Direction
    {
        get => Variable.Direction;
        set
        {
            if (value is not null)
            {
                Edit(Variable.Direction, value, v => Variable.Direction = v);
            }
        }
    }

    public string Address
    {
        get => Variable.Address;
        set => Edit(Variable.Address, value.Trim(), v => Variable.Address = v);
    }

    public string Description => Variable.Description;
}
