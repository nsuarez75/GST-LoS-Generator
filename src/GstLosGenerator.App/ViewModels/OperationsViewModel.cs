using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GstLosGenerator.Core;
using GstLosGenerator.Core.Fb;

namespace GstLosGenerator.App.ViewModels;

// Operaciones de un PLC: lista a la izquierda y ficha de la seleccionada a la derecha.
public sealed partial class OperationsViewModel : EditableViewModel, ISelectable
{
    private readonly IDialogs _dialogs;

    public Plc Plc { get; }
    public ObservableCollection<OperationViewModel> Operations { get; } = [];

    [ObservableProperty]
    public partial OperationViewModel? Selected { get; set; }

    public OperationsViewModel(Session session, IDialogs dialogs, Plc plc) : base(session)
    {
        _dialogs = dialogs;
        Plc = plc;
        foreach (var operation in plc.OrderedOperations())
        {
            Operations.Add(new OperationViewModel(session, plc, operation));
        }
        Selected = Operations.FirstOrDefault();
    }

    public bool IsEmpty => Operations.Count == 0;

    // Orden elegido arrastrando; si no, por número.
    public bool HasCustomOrder => Plc.CustomOperationOrder;

    // Lleva una operación a otra posición de la lista (arrastrándola). El orden es el de los segmentos del FB.
    public void Move(OperationViewModel operation, int target)
    {
        var index = Operations.IndexOf(operation);
        if (index < 0 || target < 0 || target >= Operations.Count || index == target)
        {
            return;
        }
        Session.Change("move-operation", () =>
        {
            var ordered = Plc.OrderedOperations();
            ordered.Remove(operation.Operation);
            ordered.Insert(target, operation.Operation);
            Plc.Operations = ordered;
            Plc.CustomOperationOrder = true;
        });
        Operations.Move(index, target);
        Selected = operation;
        OnPropertyChanged(nameof(HasCustomOrder));
    }

    [RelayCommand]
    private void SortByNumber()
    {
        Session.Change("sort-operations", () =>
        {
            Plc.CustomOperationOrder = false;
            Plc.Operations = Plc.OrderedOperations();
        });
        var selected = Selected;
        var sorted = Operations.OrderBy(o => o.Operation.Id, OperationIdComparer.Instance).ToList();
        Operations.Clear();
        foreach (var operation in sorted)
        {
            Operations.Add(operation);
        }
        Selected = selected;
        OnPropertyChanged(nameof(HasCustomOrder));
    }

    public void Select(string subject) => Selected = Operations.FirstOrDefault(o => o.Operation.Id == subject) ?? Selected;

    [RelayCommand]
    private async Task Add()
    {
        var next = Plc.Operations.Select(o => int.TryParse(o.Id, out var n) ? n : 0).DefaultIfEmpty(100).Max() + 10;
        var values = await _dialogs.Prompt(L.T("Nueva operación"), [L.T("Número")], [next.ToString(System.Globalization.CultureInfo.InvariantCulture)]);
        var id = values?[0].Trim() ?? "";
        if (id.Length == 0)
        {
            return;
        }
        if (Plc.FindOperation(id) is not null)
        {
            await _dialogs.Message(L.T("Nueva operación"), L.T("Ya hay una operación {0}.", id));
            return;
        }
        var operation = new Operation { Id = id };
        Session.Change("add-operation", () => Plc.Operations.Add(operation));
        var vm = new OperationViewModel(Session, Plc, operation);
        // Con orden propio va al final; si no, en su sitio por número.
        Operations.Insert(Plc.OrderedOperations().IndexOf(operation), vm);
        Selected = vm;
        OnPropertyChanged(nameof(IsEmpty));
    }

    [RelayCommand]
    private async Task Remove()
    {
        if (Selected is not { } vm)
        {
            return;
        }
        if (!await _dialogs.Confirm(L.T("Eliminar operación"), L.T("¿Eliminar la operación {0}? Los assets que la tienen asignada se quedan sin operación.", vm.Operation.Id), L.T("Eliminar")))
        {
            return;
        }
        Session.Change("remove-operation", () =>
        {
            Plc.Operations.Remove(vm.Operation);
            foreach (var asset in Plc.Assets.Where(a => a.Operation == vm.Operation.Id))
            {
                asset.Operation = null;
            }
        });
        Operations.Remove(vm);
        Selected = Operations.FirstOrDefault();
        OnPropertyChanged(nameof(IsEmpty));
    }
}

public sealed partial class OperationViewModel : EditableViewModel
{
    public Plc Plc { get; }
    public Operation Operation { get; }
    public ObservableCollection<OperationLocationViewModel> Locations { get; } = [];
    public ObservableCollection<AssetCheckViewModel> Assets { get; } = [];
    public IReadOnlyList<string> LocationNames { get; }

    [ObservableProperty]
    public partial string AssetFilter { get; set; } = "";

    // Al arrastrar otra operación: caería delante o detrás de esta.
    [ObservableProperty]
    public partial bool DropBefore { get; set; }

    [ObservableProperty]
    public partial bool DropAfter { get; set; }

    public OperationViewModel(Session session, Plc plc, Operation operation) : base(session)
    {
        Plc = plc;
        Operation = operation;
        LocationNames = plc.Locations.Select(l => l.Name).ToList();
        foreach (var location in operation.Locations)
        {
            Locations.Add(new OperationLocationViewModel(session, this, location));
        }
        BuildAssets();
    }

    public string Id
    {
        get => Operation.Id;
        set
        {
            var id = value.Trim();
            if (id.Length == 0 || Plc.FindOperation(id) is not null)
            {
                return;
            }
            var old = Operation.Id;
            Edit(old, id, v =>
            {
                Operation.Id = v;
                foreach (var asset in Plc.Assets.Where(a => a.Operation == old))
                {
                    asset.Operation = v;
                }
            });
            TitleChanged();
        }
    }

    // Vacío: el título del segmento lleva las locations; si no, «Operation 110: comentario».
    public string Comment
    {
        get => Operation.Comment;
        set
        {
            if (Edit(Operation.Comment, value.Trim(), v => Operation.Comment = v))
            {
                TitleChanged();
            }
        }
    }

    public string Title => Segments.OperationTitle(Operation);
    public string Summary => L.T("{0} locations · {1} assets", Operation.Locations.Count, Plc.Assets.Count(a => a.Operation == Operation.Id));

    public void TitleChanged()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Summary));
    }

    partial void OnAssetFilterChanged(string value) => BuildAssets();

    // Assets que se pueden asignar; los ya asignados primero.
    private void BuildAssets()
    {
        Assets.Clear();
        var filter = AssetFilter.Trim();
        foreach (var asset in Plc.Assets.Where(a => !a.Missing)
                     .Where(a => filter.Length == 0 || a.Element.Contains(filter, StringComparison.OrdinalIgnoreCase) || a.Type.Contains(filter, StringComparison.OrdinalIgnoreCase))
                     .OrderBy(a => a.Operation == Operation.Id ? 0 : 1))
        {
            Assets.Add(new AssetCheckViewModel(Session, this, asset));
        }
    }

    [RelayCommand]
    private void AddLocation()
    {
        var location = new OperationLocation
        {
            Location = LocationNames.FirstOrDefault(n => Operation.Locations.All(l => l.Location != n)) ?? "",
            References = [new ReferenceSignals { Reference = 1 }],
        };
        Session.Change("add-location", () => Operation.Locations.Add(location));
        Locations.Add(new OperationLocationViewModel(Session, this, location));
        TitleChanged();
    }

    [RelayCommand]
    private void RemoveLocation(OperationLocationViewModel? location)
    {
        if (location is null)
        {
            return;
        }
        Session.Change("remove-location", () => Operation.Locations.Remove(location.Location));
        Locations.Remove(location);
        TitleChanged();
    }

    [RelayCommand]
    private void MoveUp(OperationLocationViewModel? location) => Move(location, -1);

    [RelayCommand]
    private void MoveDown(OperationLocationViewModel? location) => Move(location, 1);

    private void Move(OperationLocationViewModel? location, int delta)
    {
        if (location is null)
        {
            return;
        }
        var index = Locations.IndexOf(location);
        var target = index + delta;
        if (index < 0 || target < 0 || target >= Locations.Count)
        {
            return;
        }
        Session.Change("move-location", () =>
        {
            Operation.Locations.RemoveAt(index);
            Operation.Locations.Insert(target, location.Location);
        });
        Locations.Move(index, target);
        TitleChanged();
    }
}

public sealed partial class AssetCheckViewModel(Session session, OperationViewModel owner, Asset asset) : EditableViewModel(session)
{
    public Asset Asset { get; } = asset;
    public string Element => Asset.Element;
    public string Type => Asset.Type;

    // Otra operación que ya tiene asignada; marcarlo aquí la sustituye.
    public string Other => Asset.Operation is { Length: > 0 } other && other != owner.Operation.Id ? L.T("en {0}", other) : "";

    public bool IsChecked
    {
        get => Asset.Operation == owner.Operation.Id;
        set
        {
            var target = value ? owner.Operation.Id : null;
            if (!value && Asset.Operation != owner.Operation.Id)
            {
                return;
            }
            Edit(Asset.Operation, target, v => Asset.Operation = v);
            OnPropertyChanged(nameof(Other));
            owner.TitleChanged();
        }
    }
}

public sealed partial class OperationLocationViewModel : EditableViewModel
{
    private readonly OperationViewModel _owner;

    public OperationLocation Location { get; }
    public ObservableCollection<ReferenceToggleViewModel> ReferenceToggles { get; } = [];
    public ObservableCollection<ReferenceRowViewModel> References { get; } = [];
    public IReadOnlyList<string> LocationNames => _owner.LocationNames;

    public OperationLocationViewModel(Session session, OperationViewModel owner, OperationLocation location) : base(session)
    {
        _owner = owner;
        Location = location;
        for (var r = 1; r <= ReferenceSignals.MaxReferences; r++)
        {
            ReferenceToggles.Add(new ReferenceToggleViewModel(this, r));
        }
        BuildRows();
    }

    public string? Name
    {
        get => Location.Location;
        set
        {
            if (value is not null && Edit(Location.Location, value, v => Location.Location = v))
            {
                OnPropertyChanged(nameof(LocNumber));
                _owner.TitleChanged();
            }
        }
    }

    public string LocNumber => _owner.Plc.FindLocation(Location.Location) is { } l
        ? "Loc " + l.Number.ToString(System.Globalization.CultureInfo.InvariantCulture)
        : L.T("location sin definir");

    public string VirtualLoc
    {
        get => Location.VirtualLoc;
        set => Edit(Location.VirtualLoc, value.Trim(), v => Location.VirtualLoc = v);
    }

    public string Mask => "Mask " + Location.Mask.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public bool HasReference(int reference) => Location.References.Any(r => r.Reference == reference);

    public void SetReference(int reference, bool on)
    {
        if (on == HasReference(reference))
        {
            return;
        }
        Session.Change("reference", () =>
        {
            if (on)
            {
                // Una referencia nueva copia las señales de la primera, que suele ser lo habitual.
                var first = Location.References.OrderBy(r => r.Reference).FirstOrDefault();
                Location.References.Add(new ReferenceSignals
                {
                    Reference = reference,
                    PartPresent = [.. first?.PartPresent ?? []],
                    ClampsOpen = [.. first?.ClampsOpen ?? []],
                });
                Location.References.Sort((a, b) => a.Reference.CompareTo(b.Reference));
            }
            else
            {
                Location.References.RemoveAll(r => r.Reference == reference);
            }
        });
        BuildRows();
        OnPropertyChanged(nameof(Mask));
    }

    private void BuildRows()
    {
        References.Clear();
        foreach (var signals in Location.References.OrderBy(r => r.Reference))
        {
            References.Add(new ReferenceRowViewModel(Session, signals));
        }
    }

    // Copia las señales de la primera referencia a las demás.
    [RelayCommand]
    private void CopyFirst()
    {
        var first = Location.References.OrderBy(r => r.Reference).FirstOrDefault();
        if (first is null)
        {
            return;
        }
        Session.Change("copy-first", () =>
        {
            foreach (var other in Location.References.Where(r => r != first))
            {
                other.PartPresent = [.. first.PartPresent];
                other.ClampsOpen = [.. first.ClampsOpen];
            }
        });
        BuildRows();
    }
}

public sealed partial class ReferenceToggleViewModel(OperationLocationViewModel owner, int reference) : ObservableObject
{
    public int Reference { get; } = reference;
    public string Label => Reference.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public bool IsChecked
    {
        get => owner.HasReference(Reference);
        set
        {
            owner.SetReference(Reference, value);
            OnPropertyChanged();
        }
    }
}

// Señales de una referencia. Varias señales se escriben separadas por & y se encadenan con Y.
public sealed partial class ReferenceRowViewModel(Session session, ReferenceSignals signals) : EditableViewModel(session)
{
    public string Label => L.T("Ref. {0}", signals.Reference);

    public string PartPresent
    {
        get => Join(signals.PartPresent);
        set => Edit(Join(signals.PartPresent), value, v => signals.PartPresent = Split(v));
    }

    public string ClampsOpen
    {
        get => Join(signals.ClampsOpen);
        set => Edit(Join(signals.ClampsOpen), value, v => signals.ClampsOpen = Split(v));
    }

    public static string Join(List<string> signals) => string.Join(" & ", signals);

    public static List<string> Split(string text) =>
        text.Split(['&', ';', ','], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();
}
