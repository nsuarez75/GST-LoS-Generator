// Construcción de cada tipo de segmento del FB.
using System.Xml.Linq;

namespace GstLosGenerator.Core.Fb;

// Lo que necesita la llamada al FB de un asset.
public sealed record CallInfo(
    string Element,
    string Instance,
    string Fb,
    string Udt,
    string UdtPin,
    string UdtPinSection,
    string OperationPin,
    string? OperationVariable,
    IReadOnlyList<FbParameter> Parameters)
{
    public string Title => $"{Element} [{Fb}]";
}

// Asignación de Struct TO DEVICE: #Device_61."Bool_061_o(000)_00" := #LX9_01_CELL01.ProductionStatus;
public sealed record DeviceAssignment(string Device, string Member, string Element, string Variable);

// Región de Struct TO DEVICE; las de celda llevan sub-regiones.
public sealed record SclRegion(string Name, IReadOnlyList<DeviceAssignment> Assignments, IReadOnlyList<SclRegion> Children);

public static class Segments
{
    public const string ConfigTitle = "******* Config Signals **************";
    public const string OperationsHeaderTitle = "============================ Setup Operations ======================";
    public const string CallsHeaderTitle = "============================ Call all blocks =========================";
    public const string LifeBitTitle = "Life bit";
    public static readonly string Separator1Title = new('=', 70);
    public const string StructTitle = "Struct TO DEVICE";
    public static readonly string Separator2Title = new('*', 70);
    public const string SwapFloatTitle = "SWAP Float";
    public const string SwapTitle = "SWAP";

    public const string LifeBitVariable = "LifeBit";
    public const string ClockVariable = "clock1Hz";
    public const string SwapRealFb = "Swap_Real";
    public const string SwapRealDb = "Swap_Real_DB";

    public static XElement Empty(string title, IReadOnlyList<string> cultures) =>
        FbXml.CompileUnit(FbXml.Lad, null, title, cultures);

    public static string OperationTitle(Operation operation) =>
        $"Operation {operation.Id} :{string.Join(", ", operation.Locations.Select(l => l.Location))}";

    // Segmento STL de configuración de una operación, como los del FB de ejemplo.
    public static XElement Operation(Operation operation, Plc plc, IReadOnlyList<string> cultures)
    {
        var ids = new UIds();
        var list = new XElement(FbXml.Stl + "StatementList");
        var name = operation.VariableName;
        Comment(list, ids, "Number of locations within this operation");
        Load(list, ids, operation.Locations.Count);
        Transfer(list, ids, [Comp(name), Comp("Amount")]);
        EmptyLine(list, ids);
        for (var i = 0; i < operation.Locations.Count; i++)
        {
            var location = operation.Locations[i];
            var index = i + 1;
            XElement Loc() => Comp("Location", index);
            Comment(list, ids, "number of the location involved in this operation");
            Load(list, ids, plc.FindLocation(location.Location)?.Number ?? 0);
            Transfer(list, ids, [Comp(name), Loc(), Comp("Loc")]);
            EmptyLine(list, ids);
            Comment(list, ids, "Filter the references that work with this location");
            Load(list, ids, location.Mask);
            Transfer(list, ids, [Comp(name), Loc(), Comp("Mask")]);
            EmptyLine(list, ids);
            Comment(list, ids, "Is a virtual location in this operation?");
            And(list, ids, location.VirtualLoc.Length > 0 ? [location.VirtualLoc] : []);
            Assign(list, ids, [Comp(name), Loc(), Comp("VirtualLoc")]);
            EmptyLine(list, ids);
            SignalGroups(list, ids, "Part present", location, r => r.PartPresent, "PartPresent_ref", name, index);
            SignalGroups(list, ids, "Clamps Open", location, r => r.ClampsOpen, "ClampsOpen_Ref", name, index);
        }
        return FbXml.CompileUnit(FbXml.StlLanguage, list, OperationTitle(operation), cultures);
    }

    // Las referencias con las mismas señales comparten un solo «A ...» y varios «=».
    private static void SignalGroups(XElement list, UIds ids, string comment, OperationLocation location,
        Func<ReferenceSignals, List<string>> signals, string member, string operation, int index)
    {
        if (location.References.Count == 0)
        {
            return;
        }
        Comment(list, ids, comment);
        var groups = location.References.OrderBy(r => r.Reference)
            .GroupBy(r => string.Join("\n", signals(r).Where(s => s.Trim().Length > 0)));
        foreach (var group in groups)
        {
            And(list, ids, group.Key.Length == 0 ? [] : group.Key.Split('\n'));
            foreach (var reference in group)
            {
                Assign(list, ids, [Comp(operation), Comp("Location", index), Comp(member, reference.Reference)]);
            }
        }
        EmptyLine(list, ids);
    }

    private static void Comment(XElement list, UIds ids, string text) =>
        list.Add(new XElement(FbXml.Stl + "StlStatement", new XAttribute("UId", ids.Next()),
            new XElement(FbXml.Stl + "LineComment", new XAttribute("Inserted", "false"), new XElement(FbXml.Stl + "Text", text)),
            new XElement(FbXml.Stl + "StlToken", new XAttribute("Text", "COMMENT"))));

    private static void EmptyLine(XElement list, UIds ids) =>
        list.Add(new XElement(FbXml.Stl + "StlStatement", new XAttribute("UId", ids.Next()),
            new XElement(FbXml.Stl + "StlToken", new XAttribute("Text", "EMPTY_LINE"))));

    private static void Load(XElement list, UIds ids, long value) =>
        list.Add(new XElement(FbXml.Stl + "StlStatement", new XAttribute("UId", ids.Next()),
            new XElement(FbXml.Stl + "StlToken", new XAttribute("Text", "L")),
            new XElement(FbXml.Stl + "Access", new XAttribute("Scope", "LiteralConstant"),
                StlConstant(value > short.MaxValue ? "DInt" : "Int", value))));

    private static void Transfer(XElement list, UIds ids, XElement[] components) =>
        list.Add(new XElement(FbXml.Stl + "StlStatement", new XAttribute("UId", ids.Next()),
            new XElement(FbXml.Stl + "StlToken", new XAttribute("Text", "T")),
            new XElement(FbXml.Stl + "Access", new XAttribute("Scope", "LocalVariable"),
                new XElement(FbXml.Stl + "Symbol", components))));

    private static void Assign(XElement list, UIds ids, XElement[] components) =>
        list.Add(new XElement(FbXml.Stl + "StlStatement", new XAttribute("UId", ids.Next()),
            new XElement(FbXml.Stl + "StlToken", new XAttribute("Text", "Assign")),
            new XElement(FbXml.Stl + "Access", new XAttribute("Scope", "LocalVariable"),
                new XElement(FbXml.Stl + "Symbol", components))));

    // «A señal» por cada señal; sin señales, «A alwaysFalse».
    private static void And(XElement list, UIds ids, IEnumerable<string> signals)
    {
        var any = false;
        foreach (var signal in signals.Select(s => s.Trim()).Where(s => s.Length > 0))
        {
            any = true;
            list.Add(StlAnd(ids, signal));
        }
        if (!any)
        {
            list.Add(StlAnd(ids, Naming.AlwaysFalse));
        }
    }

    private static XElement StlAnd(UIds ids, string signal)
    {
        var (local, components) = FbXml.ParseSymbol(signal);
        return new XElement(FbXml.Stl + "StlStatement", new XAttribute("UId", ids.Next()),
            new XElement(FbXml.Stl + "StlToken", new XAttribute("Text", "A")),
            new XElement(FbXml.Stl + "Access", new XAttribute("Scope", local ? "LocalVariable" : "GlobalVariable"),
                new XElement(FbXml.Stl + "Symbol", components.Select(c => Comp(c)))));
    }

    private static XElement Comp(string name, int? index = null)
    {
        var component = new XElement(FbXml.Stl + "Component", new XAttribute("Name", name));
        if (index is not null)
        {
            component.Add(new XAttribute("AccessModifier", "Array"),
                new XElement(FbXml.Stl + "Access", new XAttribute("Scope", "LiteralConstant"), StlConstant("DInt", index.Value)));
        }
        return component;
    }

    private static XElement StlConstant(string type, long value) =>
        new(FbXml.Stl + "Constant",
            new XElement(FbXml.Stl + "ConstantType", type),
            new XElement(FbXml.Stl + "ConstantValue", FbXml.Constant(value)));

    // ---------------------------------------------------------------- LAD

    // Llamada al FB de un asset: operación al pin de operación y variable del asset al pin de su UDT.
    public static XElement Call(CallInfo call, IReadOnlyList<string> cultures)
    {
        var ids = new UIds();
        var parts = new XElement(FbXml.FlgNet + "Parts");
        var wires = new XElement(FbXml.FlgNet + "Wires");
        string? operationAccess = null;
        if (call.OperationVariable is not null && call.OperationPin.Length > 0)
        {
            operationAccess = ids.Next();
            parts.Add(LadAccess(operationAccess, "LocalVariable", [call.OperationVariable]));
        }
        var assetAccess = ids.Next();
        parts.Add(LadAccess(assetAccess, "LocalVariable", [call.Element]));
        var callId = ids.Next();
        parts.Add(LadCall(callId, ids.Next(), call));
        wires.Add(Wire(ids, new XElement(FbXml.FlgNet + "Powerrail"), NameCon(callId, "en")));
        if (operationAccess is not null)
        {
            wires.Add(Wire(ids, IdentCon(operationAccess), NameCon(callId, call.OperationPin)));
        }
        wires.Add(call.UdtPinSection == "Input"
            ? Wire(ids, IdentCon(assetAccess), NameCon(callId, call.UdtPin))
            : Wire(ids, NameCon(callId, call.UdtPin), IdentCon(assetAccess)));
        OpenUnwired(wires, callId, CallParameters(call), ids.Next);
        return FbXml.CompileUnit(FbXml.Lad, new XElement(FbXml.FlgNet + "FlgNet", parts, wires), call.Title, cultures);
    }

    public static XElement LadCall(string callId, string instanceId, CallInfo call) =>
        new(FbXml.FlgNet + "Call", new XAttribute("UId", callId),
            new XElement(FbXml.FlgNet + "CallInfo", new XAttribute("Name", call.Fb), new XAttribute("BlockType", "FB"),
                new XElement(FbXml.FlgNet + "Instance", new XAttribute("Scope", "LocalVariable"), new XAttribute("UId", instanceId),
                    new XElement(FbXml.FlgNet + "Component", new XAttribute("Name", call.Instance))),
                CallParameters(call).Select(p => new XElement(FbXml.FlgNet + "Parameter",
                    new XAttribute("Name", p.Name), new XAttribute("Section", p.Section), new XAttribute("Type", p.Type)))));

    // Interfaz de la llamada: la aprendida del FB si se conoce; si no, solo los pines que se conectan.
    public static IReadOnlyList<FbParameter> CallParameters(CallInfo call)
    {
        if (call.Parameters.Count > 0)
        {
            return call.Parameters;
        }
        var list = new List<FbParameter>();
        if (call.OperationVariable is not null && call.OperationPin.Length > 0)
        {
            list.Add(new FbParameter(call.OperationPin, "Input", $"\"{Naming.OperationUdt}\""));
        }
        list.Add(new FbParameter(call.UdtPin, call.UdtPinSection, $"\"{call.Udt}\""));
        return list;
    }

    // TIA no importa una llamada con pines declarados sin conexión: los que no se cablean van a un OpenCon.
    public static void OpenUnwired(XElement wires, string callId, IEnumerable<FbParameter> parameters, Func<string> nextId)
    {
        var wired = wires.Elements(FbXml.FlgNet + "Wire")
            .SelectMany(w => w.Elements(FbXml.FlgNet + "NameCon"))
            .Where(n => (string?)n.Attribute("UId") == callId)
            .Select(n => (string)n.Attribute("Name")!)
            .ToHashSet();
        foreach (var parameter in parameters.Where(p => !wired.Contains(p.Name)))
        {
            wires.Add(new XElement(FbXml.FlgNet + "Wire", new XAttribute("UId", nextId()),
                new XElement(FbXml.FlgNet + "OpenCon", new XAttribute("UId", nextId())),
                NameCon(callId, parameter.Name)));
        }
    }

    public static bool IsOpen(XElement wire) => wire.Element(FbXml.FlgNet + "OpenCon") is not null;

    public static XElement LadAccess(string id,string scope, IEnumerable<string> components) =>
        new(FbXml.FlgNet + "Access", new XAttribute("Scope", scope), new XAttribute("UId", id),
            new XElement(FbXml.FlgNet + "Symbol", components.Select(c => new XElement(FbXml.FlgNet + "Component", new XAttribute("Name", c)))));

    public static XElement Wire(UIds ids, params XElement[] connections) =>
        new(FbXml.FlgNet + "Wire", new XAttribute("UId", ids.Next()), connections);

    public static XElement NameCon(string uid, string name) =>
        new(FbXml.FlgNet + "NameCon", new XAttribute("UId", uid), new XAttribute("Name", name));

    public static XElement IdentCon(string uid) => new(FbXml.FlgNet + "IdentCon", new XAttribute("UId", uid));

    public static XElement LifeBit(IReadOnlyList<string> cultures)
    {
        var ids = new UIds();
        string clock = ids.Next(), bit = ids.Next(), contact = ids.Next(), coil = ids.Next();
        var net = new XElement(FbXml.FlgNet + "FlgNet",
            new XElement(FbXml.FlgNet + "Parts",
                LadAccess(clock, "GlobalVariable", [ClockVariable]),
                LadAccess(bit, "LocalVariable", [LifeBitVariable]),
                new XElement(FbXml.FlgNet + "Part", new XAttribute("Name", "Contact"), new XAttribute("UId", contact)),
                new XElement(FbXml.FlgNet + "Part", new XAttribute("Name", "Coil"), new XAttribute("UId", coil))),
            new XElement(FbXml.FlgNet + "Wires",
                Wire(ids, new XElement(FbXml.FlgNet + "Powerrail"), NameCon(contact, "in")),
                Wire(ids, IdentCon(clock), NameCon(contact, "operand")),
                Wire(ids, NameCon(contact, "out"), NameCon(coil, "in")),
                Wire(ids, IdentCon(bit), NameCon(coil, "operand"))));
        return FbXml.CompileUnit(FbXml.Lad, net, LifeBitTitle, cultures);
    }

    // Swap_Real sobre cada Float de los devices, todas en paralelo desde la barra de alimentación.
    public static XElement SwapFloat(IReadOnlyList<(string Device, string Member)> floats, IReadOnlyList<string> cultures)
    {
        if (floats.Count == 0)
        {
            return Empty(SwapFloatTitle, cultures);
        }
        var ids = new UIds();
        var parts = new XElement(FbXml.FlgNet + "Parts");
        var accesses = floats.Select(f => (In: ids.Next(), Out: ids.Next(), f.Device, f.Member)).ToList();
        foreach (var a in accesses)
        {
            parts.Add(LadAccess(a.In, "LocalVariable", [a.Device, a.Member]));
            parts.Add(LadAccess(a.Out, "LocalVariable", [a.Device, a.Member]));
        }
        var calls = new List<string>();
        foreach (var _ in accesses)
        {
            var call = ids.Next();
            calls.Add(call);
            parts.Add(new XElement(FbXml.FlgNet + "Call", new XAttribute("UId", call),
                new XElement(FbXml.FlgNet + "CallInfo", new XAttribute("Name", SwapRealFb), new XAttribute("BlockType", "FB"),
                    new XElement(FbXml.FlgNet + "Instance", new XAttribute("Scope", "GlobalVariable"), new XAttribute("UId", ids.Next()),
                        new XElement(FbXml.FlgNet + "Component", new XAttribute("Name", SwapRealDb))),
                    new XElement(FbXml.FlgNet + "Parameter", new XAttribute("Name", "In"), new XAttribute("Section", "Input"), new XAttribute("Type", "Real")),
                    new XElement(FbXml.FlgNet + "Parameter", new XAttribute("Name", "OUT"), new XAttribute("Section", "InOut"), new XAttribute("Type", "Real")))));
        }
        var wires = new XElement(FbXml.FlgNet + "Wires",
            Wire(ids, [new XElement(FbXml.FlgNet + "Powerrail"), .. calls.Select(c => NameCon(c, "en"))]));
        for (var i = 0; i < accesses.Count; i++)
        {
            wires.Add(Wire(ids, IdentCon(accesses[i].In), NameCon(calls[i], "In")));
            wires.Add(Wire(ids, IdentCon(accesses[i].Out), NameCon(calls[i], "OUT")));
        }
        return FbXml.CompileUnit(FbXml.Lad, new XElement(FbXml.FlgNet + "FlgNet", parts, wires), SwapFloatTitle, cultures);
    }

    // ---------------------------------------------------------------- SCL

    public static XElement StructToDevice(IReadOnlyList<SclRegion> regions, IReadOnlyList<string> cultures)
    {
        var w = new SclWriter();
        foreach (var region in regions)
        {
            w.Region(region, 0);
        }
        return FbXml.CompileUnit(FbXml.SclLanguage, w.Root, StructTitle, cultures);
    }

    // #Device."Int16_011_o(129)" := SWAP(#Device."Int16_011_o(129)");
    public static XElement Swap(IReadOnlyList<(string Device, string Member, string SwapType)> values, IReadOnlyList<string> cultures)
    {
        var w = new SclWriter();
        foreach (var v in values)
        {
            w.Swap(v.Device, v.Member, v.SwapType);
        }
        return FbXml.CompileUnit(FbXml.SclLanguage, w.Root, SwapTitle, cultures);
    }
}

// Escribe SCL con los elementos de Openness: Token, Blank, NewLine, Text y Access.
public sealed class SclWriter
{
    private readonly UIds _ids = new();
    private static readonly XNamespace Ns = FbXml.Scl;

    public XElement Root { get; } = new(FbXml.Scl + "StructuredText");

    public SclWriter Token(string text) => Add(new XElement(Ns + "Token", new XAttribute("Text", text), Id()));

    public SclWriter Blank(int count = 1) => Add(new XElement(Ns + "Blank", new XAttribute("Num", count), Id()));

    public SclWriter NewLine(int count = 1) => Add(new XElement(Ns + "NewLine", new XAttribute("Num", count), Id()));

    public SclWriter Text(string text) => Add(new XElement(Ns + "Text", Id(), text));

    public SclWriter Add(XElement element)
    {
        Root.Add(element);
        return this;
    }

    private XAttribute Id() => new("UId", _ids.Next());

    // #a.b con los componentes separados por el token «.».
    public XElement Local(params string[] components)
    {
        var symbol = new XElement(Ns + "Symbol", Id());
        for (var i = 0; i < components.Length; i++)
        {
            if (i > 0)
            {
                symbol.Add(new XElement(Ns + "Token", new XAttribute("Text", "."), Id()));
            }
            symbol.Add(new XElement(Ns + "Component", new XAttribute("Name", components[i]), Id()));
        }
        return new XElement(Ns + "Access", new XAttribute("Scope", "LocalVariable"), Id(), symbol);
    }

    public void Region(SclRegion region, int depth)
    {
        Indent(depth);
        Token("REGION").Blank().Text(region.Name).NewLine();
        foreach (var a in region.Assignments)
        {
            Indent(depth + 1);
            Add(Local(a.Device, a.Member)).Blank().Token(":=").Blank().Add(Local(a.Element, a.Variable)).Token(";").NewLine();
        }
        for (var i = 0; i < region.Children.Count; i++)
        {
            if (i > 0 || region.Assignments.Count > 0)
            {
                NewLine();
            }
            Region(region.Children[i], depth + 1);
        }
        Indent(depth);
        Token("END_REGION").NewLine(depth == 0 ? 2 : 1);
    }

    private void Indent(int depth)
    {
        if (depth > 0)
        {
            Blank(depth * 4);
        }
    }

    public void Swap(string device, string member, string swapType)
    {
        Add(Local(device, member)).Blank().Token(":=").Blank();
        Add(new XElement(Ns + "Access", new XAttribute("Scope", "Call"), Id(),
            new XElement(Ns + "Instruction", new XAttribute("Name", "SWAP"), Id(),
                new XElement(Ns + "TemplateValue", new XAttribute("Name", "value_type"), new XAttribute("Type", "Type"), Id(), swapType),
                new XElement(Ns + "Token", new XAttribute("Text", "("), Id()),
                new XElement(Ns + "NamelessParameter", Id(), Local(device, member)),
                new XElement(Ns + "Token", new XAttribute("Text", ")"), Id()))));
        Token(";").NewLine();
    }
}
