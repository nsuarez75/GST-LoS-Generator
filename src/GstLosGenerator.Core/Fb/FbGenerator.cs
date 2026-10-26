// Genera el FB de un PLC. Sin FB actual se monta desde cero. Con FB actual:
// - lo que falta se añade en su sitio;
// - lo que ya está solo se reescribe si se ha marcado;
// - los segmentos y variables hechos a mano se conservan tal cual.
using System.Xml.Linq;

namespace GstLosGenerator.Core.Fb;

[Flags]
public enum FbParts
{
    None = 0,
    Static = 1,
    Operations = 2,
    Calls = 4,
    Struct = 8,
    Swaps = 16,
    Udts = 32,
    All = Static | Operations | Calls | Struct | Swaps | Udts,
}

public sealed class GenerateOptions
{
    // Partes que se reescriben si ya existen en el FB actual.
    public FbParts Parts { get; set; } = FbParts.None;
    // Assets marcados: sus declaraciones, llamadas y regiones se reescriben si existen.
    public HashSet<string> Assets { get; set; } = [];
    // Llamadas que se regeneran aunque se pierda algún pin conectado a mano.
    public HashSet<string> ConfirmedPinLoss { get; set; } = [];
}

// Pines conectados a mano que el FB nuevo ya no tiene.
public sealed record PinLoss(string Element, string Fb, IReadOnlyList<string> Pins);

public sealed class GenerateResult
{
    public string Xml { get; set; } = "";
    public List<string> Warnings { get; } = [];
    public List<PinLoss> PinLosses { get; } = [];
}

public static class FbGenerator
{
    public static GenerateResult Generate(Plc plc, Catalog catalog, string? currentFb, GenerateOptions options)
    {
        var plan = FbPlan.Create(plc, catalog);
        var result = new GenerateResult();
        result.Warnings.AddRange(plan.Warnings);
        var document = currentFb is null ? FbDocument.CreateEmpty() : FbDocument.Parse(currentFb);
        var fresh = currentFb is null;
        var cultures = document.Cultures;

        var generated = BuildSegments(plan, cultures);
        MergeSegments(document, generated, plan, options, fresh, result);
        MergeStatic(document, plan, options, fresh);
        EnsureTemp(document, Segments.LifeBitVariable, "Bool");

        if (!fresh)
        {
            var known = generated.Select(g => g.Key).ToHashSet();
            foreach (var segment in document.Segments.Where(s => s.Key?.StartsWith("call:", StringComparison.Ordinal) == true && !known.Contains(s.Key)))
            {
                result.Warnings.Add(L.T("El segmento «{0}» es de un asset que ya no se genera; se deja como está.", segment.Title));
            }
        }
        result.Xml = document.Write();
        return result;
    }

    private static List<(string Key, XElement Unit, string? Asset)> BuildSegments(FbPlan plan, IReadOnlyList<string> cultures)
    {
        var list = new List<(string, XElement, string?)>
        {
            (SegmentKeys.Config, Segments.Empty(Segments.ConfigTitle, cultures), null),
            (SegmentKeys.OperationsHeader, Segments.Empty(Segments.OperationsHeaderTitle, cultures), null),
        };
        foreach (var operation in plan.Operations.OrderBy(o => o.Id, OperationIdComparer.Instance))
        {
            list.Add((SegmentKeys.Operation(operation.Id), Segments.Operation(operation, plan.Plc, cultures), null));
        }
        list.Add((SegmentKeys.CallsHeader, Segments.Empty(Segments.CallsHeaderTitle, cultures), null));
        foreach (var call in plan.Calls)
        {
            list.Add((SegmentKeys.Call(call.Element), Segments.Call(call, cultures), call.Element));
        }
        list.Add((SegmentKeys.LifeBit, Segments.LifeBit(cultures), null));
        list.Add((SegmentKeys.Separator1, Segments.Empty(Segments.Separator1Title, cultures), null));
        list.Add((SegmentKeys.Struct, Segments.StructToDevice(plan.Regions, cultures), null));
        list.Add((SegmentKeys.Separator2, Segments.Empty(Segments.Separator2Title, cultures), null));
        list.Add((SegmentKeys.SwapFloat, Segments.SwapFloat(plan.Floats, cultures), null));
        list.Add((SegmentKeys.Swap, Segments.Swap(plan.Swaps, cultures), null));
        return list;
    }

    private static void MergeSegments(FbDocument document, List<(string Key, XElement Unit, string? Asset)> generated,
        FbPlan plan, GenerateOptions options, bool fresh, GenerateResult result)
    {
        var segments = document.Segments;
        FbSegment? previous = null;
        foreach (var (key, unit, asset) in generated)
        {
            var existing = document.Find(key);
            if (existing is null)
            {
                // Lo que falta va justo detrás del anterior segmento de la app, o al principio.
                var segment = new FbSegment(unit, key);
                var at = previous is null ? 0 : segments.IndexOf(previous) + 1;
                segments.Insert(at, segment);
                previous = segment;
                continue;
            }
            previous = existing;
            if (fresh || !ShouldRewrite(key, asset, options))
            {
                continue;
            }
            if (key.StartsWith("call:", StringComparison.Ordinal))
            {
                var call = plan.Calls.First(c => c.Element == asset);
                var loss = CallMerge.Update(existing.Unit, call, options.ConfirmedPinLoss.Contains(call.Element));
                if (loss is not null)
                {
                    result.PinLosses.Add(loss);
                    result.Warnings.Add(L.T("{0}: el FB {1} no tiene los pines {2}, conectados a mano; no se regenera hasta que lo confirmes.",
                        loss.Element, loss.Fb, string.Join(", ", loss.Pins)));
                }
            }
            else if (key == SegmentKeys.Struct)
            {
                RegionMerge.Update(existing.Unit, unit, plan, options.Assets);
            }
            else
            {
                existing.Unit = unit;
            }
        }
    }

    private static bool ShouldRewrite(string key, string? asset, GenerateOptions options)
    {
        var parts = options.Parts;
        if (key.StartsWith("op:", StringComparison.Ordinal))
        {
            return parts.HasFlag(FbParts.Operations);
        }
        if (key.StartsWith("call:", StringComparison.Ordinal))
        {
            return parts.HasFlag(FbParts.Calls) && asset is not null && options.Assets.Contains(asset);
        }
        return key switch
        {
            SegmentKeys.Struct => parts.HasFlag(FbParts.Struct),
            SegmentKeys.SwapFloat or SegmentKeys.Swap => parts.HasFlag(FbParts.Swaps),
            _ => false,
        };
    }

    // Estática: lo que falta se añade detrás de la anterior variable de la app; lo marcado se reescribe.
    private static void MergeStatic(FbDocument document, FbPlan plan, GenerateOptions options, bool fresh)
    {
        var section = document.Section("Static");
        XElement? previous = null;
        foreach (var member in plan.Static)
        {
            var existing = section.Elements(FbXml.Interface + "Member").FirstOrDefault(m => (string?)m.Attribute("Name") == member.Name);
            var element = StaticElement(member);
            if (existing is null)
            {
                if (previous is null)
                {
                    section.AddFirst(element);
                }
                else
                {
                    previous.AddAfterSelf(element);
                }
                previous = element;
                continue;
            }
            var rewrite = !fresh && options.Parts.HasFlag(FbParts.Static)
                && (member.Asset is null || options.Assets.Contains(member.Asset))
                && (string?)existing.Attribute("Datatype") != member.Datatype;
            if (rewrite)
            {
                existing.ReplaceWith(element);
                previous = element;
            }
            else
            {
                previous = existing;
            }
        }
    }

    private static XElement StaticElement(StaticMember member)
    {
        var element = new XElement(FbXml.Interface + "Member",
            new XAttribute("Name", member.Name),
            new XAttribute("Datatype", member.Datatype));
        if (member.Remanent)
        {
            element.Add(new XAttribute("Remanence", "SetInIDB"));
        }
        element.Add(new XAttribute("Accessibility", "Public"));
        return element;
    }

    private static void EnsureTemp(FbDocument document, string name, string type)
    {
        var temp = document.Section("Temp");
        if (!temp.Elements(FbXml.Interface + "Member").Any(m => (string?)m.Attribute("Name") == name))
        {
            temp.Add(new XElement(FbXml.Interface + "Member", new XAttribute("Name", name), new XAttribute("Datatype", type)));
        }
    }
}

// Ordena operaciones por número cuando lo son (110 antes que 1100) y por texto si no (DMC).
public sealed class OperationIdComparer : IComparer<string>
{
    public static readonly OperationIdComparer Instance = new();

    public int Compare(string? x, string? y)
    {
        var xn = long.TryParse(x, out var a);
        var yn = long.TryParse(y, out var b);
        if (xn && yn)
        {
            return a.CompareTo(b);
        }
        if (xn != yn)
        {
            return xn ? -1 : 1;
        }
        return string.CompareOrdinal(x, y);
    }
}
