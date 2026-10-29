// «Leer FB Tipo…» en el catálogo: rellena la equivalencia y la interfaz de un tipo a partir de un XML
// exportado de TIA, sin tocar el proyecto. El XML puede ser un FB que llama al del tipo (cada llamada a un
// FB distinto es una opción) o el propio FB del tipo (su interfaz).
using System.Xml.Linq;

namespace GstLosGenerator.Core.Fb;

// Lo que se aprende de una opción del XML; null en lo que no se sabe (se deja como está).
public sealed record LearnSource(string Label, string Fb, string? Udt, string? UdtPin, string? UdtPinSection,
    string? OperationPin, IReadOnlyList<FbParameter> Parameters);

public static class TypeLearner
{
    private static readonly XNamespace Lad = FbXml.FlgNet;

    public static List<LearnSource> Sources(string xml)
    {
        var document = FbDocument.Parse(xml);
        var statics = document.Section("Static").Elements(FbXml.Interface + "Member")
            .ToDictionary(m => (string)m.Attribute("Name")!, m => ((string?)m.Attribute("Datatype"))?.Trim('"'), StringComparer.Ordinal);
        var sources = new List<LearnSource>();
        foreach (var segment in document.Segments)
        {
            if (FromCall(segment, statics) is { } source && sources.All(s => s.Fb != source.Fb))
            {
                sources.Add(source);
            }
        }
        if (sources.Count == 0 && FromInterface(document) is { } own)
        {
            sources.Add(own);
        }
        return sources;
    }

    // Llamada a un FB en un segmento LAD: pines, el UDT conectado y la operación conectada.
    private static LearnSource? FromCall(FbSegment segment, Dictionary<string, string?> statics)
    {
        var net = FbXml.Source(segment.Unit);
        var parts = net?.Element(Lad + "Parts");
        var call = parts?.Elements(Lad + "Call").FirstOrDefault(c => (string?)c.Element(Lad + "CallInfo")?.Attribute("BlockType") == "FB");
        if (net is null || parts is null || call is null)
        {
            return null;
        }
        var info = call.Element(Lad + "CallInfo")!;
        var fb = (string)info.Attribute("Name")!;
        var callId = (string)call.Attribute("UId")!;
        var parameters = info.Elements(Lad + "Parameter")
            .Select(p => new FbParameter((string)p.Attribute("Name")!, (string?)p.Attribute("Section") ?? "", (string?)p.Attribute("Type") ?? ""))
            .ToList();
        var element = FbDocument.CallElement(segment.Unit);
        string? udtPin = null, udt = null, operationPin = null;
        var candidates = new List<(string Pin, string Variable)>();
        foreach (var wire in net.Element(Lad + "Wires")?.Elements(Lad + "Wire") ?? [])
        {
            var pin = (string?)wire.Elements(Lad + "NameCon").FirstOrDefault(n => (string?)n.Attribute("UId") == callId)?.Attribute("Name");
            var ident = (string?)wire.Element(Lad + "IdentCon")?.Attribute("UId");
            var access = parts.Elements(Lad + "Access").FirstOrDefault(a => (string?)a.Attribute("UId") == ident);
            var components = access?.Element(Lad + "Symbol")?.Elements(Lad + "Component").Select(c => (string)c.Attribute("Name")!).ToList();
            if (pin is null || components is not { Count: 1 })
            {
                continue;
            }
            if (components[0].StartsWith("Operation_", StringComparison.Ordinal))
            {
                operationPin = pin;
            }
            else if (components[0] == element)
            {
                udtPin = pin;
                udt = statics.GetValueOrDefault(element);
            }
            else
            {
                candidates.Add((pin, components[0]));
            }
        }
        // Sin título «asset [FB]»: el pin del UDT es el que va a una variable estática de tipo UDT.
        if (udtPin is null && candidates.FirstOrDefault(c => statics.GetValueOrDefault(c.Variable) is { Length: > 0 }) is { Pin: not null } found)
        {
            udtPin = found.Pin;
            udt = statics[found.Variable];
        }
        var section = parameters.FirstOrDefault(p => p.Name == udtPin)?.Section;
        udt ??= parameters.FirstOrDefault(p => p.Name == udtPin)?.Type.Trim('"');
        var label = L.T("Llamada a {0} ({1})", fb, element ?? segment.Title);
        return new LearnSource(label, fb, udt, udtPin, section, operationPin, parameters);
    }

    // El propio FB del tipo: su interfaz. El pin de la operación es el de tipo I40_FixtureCfg_UDT
    // y el del UDT, el único otro pin de salida o entrada/salida de tipo UDT, si lo hay.
    private static LearnSource? FromInterface(FbDocument document)
    {
        var parameters = new[] { "Input", "Output", "InOut" }
            .SelectMany(section => document.Section(section).Elements(FbXml.Interface + "Member")
                .Select(m => new FbParameter((string)m.Attribute("Name")!, section, (string?)m.Attribute("Datatype") ?? "")))
            .ToList();
        if (document.Name.Length == 0 || parameters.Count == 0)
        {
            return null;
        }
        var operation = parameters.FirstOrDefault(p => p.Type.Trim('"') == Naming.OperationUdt);
        var udts = parameters.Where(p => p != operation && p.Section is "Output" or "InOut" && p.Type.StartsWith('"')).ToList();
        var udt = udts.Count == 1 ? udts[0] : null;
        return new LearnSource(L.T("Interfaz del propio FB {0}", document.Name), document.Name, udt?.Type.Trim('"'), udt?.Name, udt?.Section,
            operation?.Name, parameters);
    }

    // Lo que cambiaría en el tipo, para enseñarlo antes de aplicar.
    public static List<string> Differences(AssetType type, LearnSource source)
    {
        var changes = new List<string>();
        if (type.NoFb)
        {
            changes.Add(L.T("Deja de ser «Sin FB»"));
        }
        Compare(changes, L.T("FB al que se llama"), type.Fb, source.Fb);
        Compare(changes, L.T("UDT de la variable del asset"), type.Udt, source.Udt);
        Compare(changes, L.T("Pin del FB para el UDT"), type.UdtPin, source.UdtPin);
        Compare(changes, L.T("Sección de ese pin"), type.UdtPinSection, source.UdtPinSection);
        Compare(changes, L.T("Pin de la operación"), type.OperationPin, source.OperationPin);
        if (source.Parameters.Count > 0 && !type.Parameters.SequenceEqual(source.Parameters))
        {
            changes.Add(L.T("Interfaz del FB: {0} pines → {1} pines", type.Parameters.Count, source.Parameters.Count));
        }
        return changes;
    }

    private static void Compare(List<string> changes, string field, string current, string? learned)
    {
        if (learned is not null && learned != current)
        {
            changes.Add($"{field}: {(current.Length == 0 ? "—" : current)} → {learned}");
        }
    }

    public static void Apply(AssetType type, LearnSource source)
    {
        type.NoFb = false;
        type.Fb = source.Fb;
        type.Udt = source.Udt ?? type.Udt;
        type.UdtPin = source.UdtPin ?? type.UdtPin;
        type.UdtPinSection = source.UdtPinSection ?? type.UdtPinSection;
        type.OperationPin = source.OperationPin ?? type.OperationPin;
        if (source.Parameters.Count > 0)
        {
            type.Parameters = [.. source.Parameters];
        }
    }
}
