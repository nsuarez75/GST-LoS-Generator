// «Leer FB»: saca de un FB exportado las operaciones, qué operación va a cada asset
// y el FB y UDT que llama cada asset, para adaptar la app a lo que hay en TIA.
using System.Xml.Linq;

namespace GstLosGenerator.Core.Fb;

public sealed record ReadCall(string Element, string Fb, string? Udt, string? UdtPin, string? UdtPinSection,
    string? OperationPin, string? Operation, IReadOnlyList<FbParameter> Parameters);

public sealed record ReadLocation(string Name, int Number);

public sealed class FbReadResult
{
    public List<Operation> Operations { get; } = [];
    public List<ReadLocation> Locations { get; } = [];
    public List<ReadCall> Calls { get; } = [];
}

public static class FbReader
{
    private static readonly XNamespace Stl = FbXml.Stl;
    private static readonly XNamespace Lad = FbXml.FlgNet;

    public static FbReadResult Read(string xml)
    {
        var document = FbDocument.Parse(xml);
        var result = new FbReadResult();
        var udts = document.Section("Static").Elements(FbXml.Interface + "Member")
            .ToDictionary(m => (string)m.Attribute("Name")!, m => ((string?)m.Attribute("Datatype"))?.Trim('"'), StringComparer.Ordinal);
        foreach (var segment in document.Segments)
        {
            if (segment.Key?.StartsWith("op:", StringComparison.Ordinal) == true)
            {
                ReadOperation(segment, result);
            }
            else if (segment.Key?.StartsWith("call:", StringComparison.Ordinal) == true)
            {
                if (ReadCall(segment, udts) is { } call)
                {
                    result.Calls.Add(call);
                }
            }
        }
        return result;
    }

    private static void ReadOperation(FbSegment segment, FbReadResult result)
    {
        var id = FbDocument.OperationOf(segment.Unit)!;
        var operation = new Operation { Id = id };
        var title = segment.Title;
        var colon = title.IndexOf(':');
        var names = colon < 0 ? [] : title[(colon + 1)..].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        long? loaded = null;
        var condition = new List<string>();
        var conditionDone = false;
        foreach (var statement in FbXml.Source(segment.Unit)!.Elements(Stl + "StlStatement"))
        {
            var token = (string?)statement.Element(Stl + "StlToken")?.Attribute("Text");
            var access = statement.Element(Stl + "Access");
            switch (token)
            {
                case "L":
                    loaded = long.TryParse(access?.Descendants(Stl + "ConstantValue").FirstOrDefault()?.Value, out var v) ? v : null;
                    break;
                case "A":
                    if (conditionDone)
                    {
                        condition = [];
                        conditionDone = false;
                    }
                    var signal = access is null ? "" : FbXml.SymbolText(access);
                    if (signal.Length > 0 && signal != Naming.AlwaysFalse)
                    {
                        condition.Add(signal);
                    }
                    break;
                case "T":
                    Store(operation, Path(access), loaded, names, result);
                    break;
                case "Assign":
                    Assign(operation, Path(access), condition);
                    conditionDone = true;
                    break;
            }
        }
        result.Operations.Add(operation);
    }

    // Componentes del destino: Operation_110.Location[1].PartPresent_ref[2] -> [(Operation_110,null),(Location,1),(PartPresent_ref,2)].
    private static List<(string Name, int? Index)> Path(XElement? access) =>
        access?.Element(Stl + "Symbol")?.Elements(Stl + "Component")
            .Select(c => ((string)c.Attribute("Name")!, int.TryParse(c.Descendants(Stl + "ConstantValue").FirstOrDefault()?.Value, out var i) ? (int?)i : null))
            .ToList() ?? [];

    private static OperationLocation LocationAt(Operation operation, int index)
    {
        while (operation.Locations.Count < index)
        {
            operation.Locations.Add(new OperationLocation());
        }
        return operation.Locations[index - 1];
    }

    private static void Store(Operation operation, List<(string Name, int? Index)> path, long? value, string[] names, FbReadResult result)
    {
        if (value is null || path.Count < 2)
        {
            return;
        }
        if (path is [_, ("Amount", _)])
        {
            return;
        }
        if (path is not [_, ("Location", { } index), (var field, _)])
        {
            return;
        }
        var location = LocationAt(operation, index);
        if (field == "Loc")
        {
            var number = (int)value.Value;
            var name = index <= names.Length ? names[index - 1] : "Loc" + number;
            location.Location = name;
            if (!result.Locations.Any(l => l.Name == name))
            {
                result.Locations.Add(new ReadLocation(name, number));
            }
        }
        else if (field == "Mask")
        {
            for (var bit = 1; bit <= 32; bit++)
            {
                if ((value.Value & (1L << (bit - 1))) != 0 && !location.References.Any(r => r.Reference == bit))
                {
                    location.References.Add(new ReferenceSignals { Reference = bit });
                }
            }
        }
    }

    private static void Assign(Operation operation, List<(string Name, int? Index)> path, List<string> condition)
    {
        if (path is not [_, ("Location", { } index), (var field, var reference)])
        {
            return;
        }
        var location = LocationAt(operation, index);
        if (field == "VirtualLoc")
        {
            location.VirtualLoc = string.Join(" ", condition);
            return;
        }
        if (reference is null)
        {
            return;
        }
        var signals = location.References.FirstOrDefault(r => r.Reference == reference);
        if (signals is null)
        {
            signals = new ReferenceSignals { Reference = reference.Value };
            location.References.Add(signals);
        }
        if (field == "PartPresent_ref")
        {
            signals.PartPresent = [.. condition];
        }
        else if (field == "ClampsOpen_Ref")
        {
            signals.ClampsOpen = [.. condition];
        }
    }

    private static ReadCall? ReadCall(FbSegment segment, Dictionary<string, string?> udts)
    {
        var element = FbDocument.CallElement(segment.Unit)!;
        var net = FbXml.Source(segment.Unit)!;
        var parts = net.Element(Lad + "Parts");
        var wires = net.Element(Lad + "Wires");
        var call = parts?.Elements(Lad + "Call").FirstOrDefault(c => (string?)c.Element(Lad + "CallInfo")?.Attribute("BlockType") == "FB");
        if (parts is null || wires is null || call is null)
        {
            return null;
        }
        var info = call.Element(Lad + "CallInfo")!;
        var callId = (string)call.Attribute("UId")!;
        var parameters = info.Elements(Lad + "Parameter")
            .Select(p => new FbParameter((string)p.Attribute("Name")!, (string?)p.Attribute("Section") ?? "", (string?)p.Attribute("Type") ?? ""))
            .ToList();
        string? udtPin = null, operationPin = null, operation = null;
        foreach (var wire in wires.Elements(Lad + "Wire"))
        {
            var pin = wire.Elements(Lad + "NameCon").FirstOrDefault(n => (string?)n.Attribute("UId") == callId);
            var ident = (string?)wire.Element(Lad + "IdentCon")?.Attribute("UId");
            if (pin is null || ident is null)
            {
                continue;
            }
            var access = parts.Elements(Lad + "Access").FirstOrDefault(a => (string?)a.Attribute("UId") == ident);
            var components = access?.Element(Lad + "Symbol")?.Elements(Lad + "Component").Select(c => (string)c.Attribute("Name")!).ToList();
            if (components is not { Count: 1 })
            {
                continue;
            }
            if (components[0] == element)
            {
                udtPin = (string)pin.Attribute("Name")!;
            }
            else if (components[0].StartsWith("Operation_", StringComparison.Ordinal))
            {
                operationPin = (string)pin.Attribute("Name")!;
                operation = components[0]["Operation_".Length..];
            }
        }
        var section = parameters.FirstOrDefault(p => p.Name == udtPin)?.Section;
        return new ReadCall(element, (string)info.Attribute("Name")!, udts.GetValueOrDefault(element), udtPin, section,
            operationPin, operation, parameters);
    }
}
