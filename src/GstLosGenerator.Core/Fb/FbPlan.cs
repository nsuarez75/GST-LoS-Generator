// Todo lo que la app pone en el FB de un PLC, calculado a partir del proyecto y del catálogo:
// variables de la estática, operaciones, llamadas, regiones de Struct TO DEVICE y swaps.
namespace GstLosGenerator.Core.Fb;

public sealed record StaticMember(string Name, string Datatype, bool Remanent, string? Asset);

public sealed class FbPlan
{
    public Plc Plc { get; }
    public List<StaticMember> Static { get; } = [];
    public List<Operation> Operations { get; } = [];
    public List<CallInfo> Calls { get; } = [];
    public List<SclRegion> Regions { get; } = [];
    // Para cada región de Struct TO DEVICE, los assets cuyas variables lleva.
    public Dictionary<string, HashSet<string>> RegionAssets { get; } = [];
    // Para cada asset de un tipo sin FB, las líneas que escribe en General.
    public Dictionary<string, List<DeviceAssignment>> DirectWrites { get; } = [];
    public List<(string Device, string Member)> Floats { get; } = [];
    public List<(string Device, string Member, string SwapType)> Swaps { get; } = [];
    public List<string> Warnings { get; } = [];

    private FbPlan(Plc plc) => Plc = plc;

    public static FbPlan Create(Plc plc, Catalog catalog)
    {
        var plan = new FbPlan(plc);
        plan.Build(catalog);
        return plan;
    }

    // FB, UDT y pines de un asset; null si le falta la equivalencia.
    public static CallInfo? ResolveCall(Plc plc, Asset asset, Catalog catalog)
    {
        var type = catalog.Find(asset.Type);
        if (type is null)
        {
            return null;
        }
        var fb = string.IsNullOrWhiteSpace(asset.FbOverride) ? type.Fb : asset.FbOverride;
        var udt = string.IsNullOrWhiteSpace(asset.UdtOverride) ? type.Udt : asset.UdtOverride;
        if (fb.Length == 0 || udt.Length == 0 || type.UdtPin.Length == 0)
        {
            return null;
        }
        var operation = asset.Operation is { Length: > 0 } id && plc.FindOperation(id) is { } op ? op.VariableName : null;
        return new CallInfo(asset.Element, Naming.WithoutLinePrefix(asset.Element), fb, udt, type.UdtPin,
            type.UdtPinSection, type.OperationPin, operation, type.Parameters);
    }

    private void Build(Catalog catalog)
    {
        var assets = Plc.Assets.Where(a => !a.Missing).ToList();
        var tree = new AssetTree(assets);
        var included = new HashSet<string>();
        var direct = new Dictionary<string, AssetType>();
        foreach (var asset in tree.InOrder())
        {
            if (catalog.Find(asset.Type) is { NoFb: true } noFb)
            {
                direct[asset.Element] = noFb;
                continue;
            }
            var call = ResolveCall(Plc, asset, catalog);
            if (call is null)
            {
                Warnings.Add(L.T("{0}: el tipo {1} no tiene equivalencia; no se genera.", asset.Element, asset.Type));
                continue;
            }
            if (asset.Operation is { Length: > 0 } && call.OperationVariable is null)
            {
                Warnings.Add(L.T("{0}: la operación {1} no existe; se llama sin operación.", asset.Element, asset.Operation));
            }
            included.Add(asset.Element);
            Calls.Add(call);
        }

        foreach (var device in Plc.Devices)
        {
            Static.Add(new(Plc.DeviceName(device.Number), $"\"{Naming.DeviceUdt(Plc.Number, device.Number)}\"", true, null));
        }
        foreach (var call in Calls)
        {
            Static.Add(new(call.Element, $"\"{call.Udt}\"", true, call.Element));
        }
        foreach (var call in Calls)
        {
            Static.Add(new(call.Instance, $"\"{call.Fb}\"", false, call.Element));
        }
        Operations.AddRange(Plc.OrderedOperations());
        foreach (var operation in Operations)
        {
            Static.Add(new(operation.VariableName, $"\"{Naming.OperationUdt}\"", true, null));
        }

        BuildRegions(tree, included, direct);

        foreach (var device in Plc.Devices)
        {
            var name = Plc.DeviceName(device.Number);
            foreach (var address in device.Addresses)
            {
                if (DataTypes.IsFloat(address.DataType))
                {
                    Floats.Add((name, address.Name));
                }
                else if (DataTypes.SwapType(address.DataType) is { } swap)
                {
                    Swaps.Add((name, address.Name, swap));
                }
            }
        }
    }

    // Los assets de tipos sin FB van todos a General, estén donde estén en el árbol.
    private void BuildRegions(AssetTree tree, HashSet<string> included, Dictionary<string, AssetType> direct)
    {
        var general = new List<DeviceAssignment>();
        var generalAssets = new HashSet<string>();
        foreach (var root in tree.Roots)
        {
            if (root.Kind != AssetKind.Cell)
            {
                foreach (var asset in tree.Descendants(root).Prepend(root))
                {
                    general.AddRange(Assignments(asset, included));
                    generalAssets.Add(asset.Element);
                }
                continue;
            }
            var children = new List<SclRegion>();
            var stateName = Naming.CellStateRegion(root.Element);
            children.Add(new SclRegion(stateName, Assignments(root, included), []));
            RegionAssets[stateName] = [root.Element];
            foreach (var child in tree.Children(root))
            {
                var members = tree.Descendants(child).Prepend(child).ToList();
                var name = Naming.OperationRegion(child.Element);
                children.Add(new SclRegion(name, members.SelectMany(a => Assignments(a, included)).ToList(), []));
                RegionAssets[name] = members.Select(a => a.Element).ToHashSet();
            }
            Regions.Add(new SclRegion(root.Element, [], children));
            RegionAssets[root.Element] = [];
        }
        foreach (var asset in tree.InOrder().Where(a => direct.ContainsKey(a.Element)))
        {
            var writes = DirectAssignments(asset, direct[asset.Element]);
            DirectWrites[asset.Element] = writes;
            general.AddRange(writes);
            generalAssets.Add(asset.Element);
        }
        Regions.Insert(0, new SclRegion("General", general, []));
        RegionAssets["General"] = generalAssets;
    }

    private List<DeviceAssignment> Assignments(Asset asset, HashSet<string> included) =>
        included.Contains(asset.Element) ? Assignments(asset, v => SclPath.Local(asset.Element, v.Name)) : [];

    // Tipo sin FB: cada variable sale de su origen en el catálogo; sin origen no se escribe.
    private List<DeviceAssignment> DirectAssignments(Asset asset, AssetType type) =>
        Assignments(asset, v =>
        {
            var source = type.Variables.FirstOrDefault(t => t.Name == v.Name)?.Source ?? "";
            if (source.Trim().Length == 0)
            {
                return null;
            }
            var path = SclPath.Parse(source);
            if (path is null)
            {
                Warnings.Add(L.T("{0}.{1}: el origen «{2}» no es una variable válida; no se escribe en Struct TO DEVICE.", asset.Element, v.Name, source));
            }
            return path;
        });

    private List<DeviceAssignment> Assignments(Asset asset, Func<AssetVariable, SclPath?> source)
    {
        var list = new List<DeviceAssignment>();
        foreach (var variable in asset.Variables.Where(v => v.IsOutput))
        {
            if (source(variable) is not { } path)
            {
                continue;
            }
            if (!Naming.TryParseAddress(variable.Address, out var parts) || Plc.FindDevice(parts.Device) is not { } device)
            {
                Warnings.Add(L.T("{0}.{1}: la dirección {2} no está en ningún device; no se escribe en Struct TO DEVICE.", asset.Element, variable.Name, variable.Address));
                continue;
            }
            var member = device.Addresses.FirstOrDefault(a => a.SheetName == variable.Address || a.Name == variable.Address)?.Name ?? variable.Address;
            list.Add(new DeviceAssignment(Plc.DeviceName(device.Number), member, path, variable.Disabled));
        }
        return list;
    }
}
