// Adapta la configuración de un PLC a lo leído de su FB. Primero se calcula la lista de diferencias
// para enseñársela al usuario; al aplicar, lo del FB gana y el FB queda como «FB actual» del PLC.
namespace GstLosGenerator.Core.Fb;

public sealed class FbSyncPlan
{
    private readonly Plc _plc;
    private readonly Catalog _catalog;
    private readonly FbReadResult _read;
    private readonly string _xml;
    private readonly string? _file;
    private readonly List<Action> _actions = [];

    public List<string> Changes { get; } = [];
    public List<string> Warnings { get; } = [];

    private FbSyncPlan(Plc plc, Catalog catalog, FbReadResult read, string xml, string? file)
    {
        _plc = plc;
        _catalog = catalog;
        _read = read;
        _xml = xml;
        _file = file;
    }

    public static FbSyncPlan Create(Plc plc, Catalog catalog, string xml, string? file = null)
    {
        var plan = new FbSyncPlan(plc, catalog, FbReader.Read(xml, plc.Locations), xml, file);
        plan.Compute();
        return plan;
    }

    private void Compute()
    {
        foreach (var location in _read.Locations)
        {
            var existing = _plc.FindLocation(location.Name);
            if (existing is null)
            {
                Changes.Add(L.T("Location nueva: {0} ({1})", location.Name, location.Number));
                _actions.Add(() => _plc.Locations.Add(new Location { Name = location.Name, Number = location.Number }));
            }
            else if (existing.Number != location.Number)
            {
                Changes.Add(L.T("Location {0}: número {1} → {2}", location.Name, existing.Number, location.Number));
                _actions.Add(() => existing.Number = location.Number);
            }
        }
        foreach (var operation in _read.Operations)
        {
            var existing = _plc.FindOperation(operation.Id);
            if (existing is null)
            {
                Changes.Add(L.T("Operación nueva: {0}", operation.Id));
                _actions.Add(() => _plc.Operations.Add(operation));
            }
            else
            {
                if (Storage.ToJson(existing.Locations) != Storage.ToJson(operation.Locations))
                {
                    Changes.Add(L.T("Operación {0}: locations, referencias o señales distintas", operation.Id));
                    _actions.Add(() => existing.Locations = operation.Locations);
                }
                if (existing.Comment.Trim() != operation.Comment)
                {
                    Changes.Add(L.T("Operación {0}: comentario «{1}» → «{2}»", operation.Id, existing.Comment.Trim(), operation.Comment));
                    _actions.Add(() => existing.Comment = operation.Comment);
                }
            }
        }
        CompareOrder();
        foreach (var call in _read.Calls)
        {
            CompareCall(call);
        }
        _actions.Add(() =>
        {
            _plc.CurrentFb = _xml;
            _plc.CurrentFbFile = _file;
        });
    }

    // Las operaciones toman el orden de sus segmentos en el FB; las que el FB no tiene van detrás.
    private void CompareOrder()
    {
        var current = _plc.OrderedOperations().Select(o => o.Id).ToList();
        var read = _read.Operations.Select(o => o.Id).ToList();
        var added = read.Where(id => !current.Contains(id)).ToList();
        var after = read.Concat(current.Where(id => !read.Contains(id))).ToList();
        // Lo que quedaría sin tocar el orden: las nuevas detrás o, sin orden propio, todas por número.
        var unchanged = _plc.CustomOperationOrder ? current.Concat(added).ToList() : [.. current.Concat(added).Order(OperationIdComparer.Instance)];
        if (read.SequenceEqual(unchanged.Where(read.Contains)))
        {
            return;
        }
        Changes.Add(L.T("Orden de las operaciones: {0}", string.Join(", ", after)));
        _actions.Add(() =>
        {
            _plc.Operations = [.. after.Select(id => _plc.FindOperation(id)!)];
            _plc.CustomOperationOrder = !after.SequenceEqual(after.Order(OperationIdComparer.Instance));
        });
    }

    private void CompareCall(ReadCall call)
    {
        var asset = _plc.FindAsset(call.Element);
        if (asset is null)
        {
            Warnings.Add(L.T("El FB llama a {0}, que no está en el proyecto; no se crea.", call.Element));
            return;
        }
        if ((asset.Operation ?? "") != (call.Operation ?? ""))
        {
            Changes.Add(L.T("{0}: operación {1} → {2}", asset.Element, Show(asset.Operation), Show(call.Operation)));
            _actions.Add(() => asset.Operation = call.Operation);
        }
        var type = _catalog.Find(asset.Type);
        if (type is null)
        {
            Warnings.Add(L.T("{0}: su tipo {1} no está en el catálogo.", asset.Element, asset.Type));
            return;
        }
        if (call.Udt is null || call.UdtPin is null)
        {
            Warnings.Add(L.T("{0}: no se ve a qué pin va su variable; no se toca su equivalencia.", asset.Element));
            return;
        }
        if (!type.HasEquivalence)
        {
            Changes.Add(L.T("Tipo {0}: equivalencia aprendida del FB (FB {1}, UDT {2}, pin {3})", type.Name, call.Fb, call.Udt, call.UdtPin));
            _actions.Add(() => Learn(type, call));
            return;
        }
        var fb = call.Fb == type.Fb ? null : call.Fb;
        var udt = call.Udt == type.Udt ? null : call.Udt;
        if (fb != Empty(asset.FbOverride) || udt != Empty(asset.UdtOverride))
        {
            Changes.Add(L.T("{0}: llama a FB {1} con UDT {2}, distinto de su tipo", asset.Element, call.Fb, call.Udt));
            _actions.Add(() =>
            {
                asset.FbOverride = fb;
                asset.UdtOverride = udt;
            });
        }
        if (call.Fb == type.Fb && type.Parameters.Count == 0 && call.Parameters.Count > 0)
        {
            _actions.Add(() => type.Parameters = [.. call.Parameters]);
        }
    }

    private static void Learn(AssetType type, ReadCall call)
    {
        type.Fb = call.Fb;
        type.Udt = call.Udt!;
        type.UdtPin = call.UdtPin!;
        type.UdtPinSection = call.UdtPinSection ?? "Output";
        if (call.OperationPin is not null)
        {
            type.OperationPin = call.OperationPin;
        }
        type.Parameters = [.. call.Parameters];
    }

    private static string? Empty(string? text) => string.IsNullOrWhiteSpace(text) ? null : text;

    private static string Show(string? operation) => string.IsNullOrEmpty(operation) ? "—" : operation;

    public void Apply()
    {
        foreach (var action in _actions)
        {
            action();
        }
    }
}
