// Regenera Struct TO DEVICE región a región: solo se reescriben las regiones que la app reconoce
// por nombre (y de los assets marcados); las que se añadieron a mano se conservan donde estaban.
using System.Xml.Linq;

namespace GstLosGenerator.Core.Fb;

public static class RegionMerge
{
    private static readonly XNamespace Ns = FbXml.Scl;

    // Un trozo del SCL: una región (con nombre) o contenido suelto entre regiones.
    private sealed record Block(string? Name, List<XElement> Items);

    public static void Update(XElement existingUnit, XElement generatedUnit, FbPlan plan, IReadOnlySet<string> selectedAssets)
    {
        var existing = FbXml.Source(existingUnit);
        if (existing is null || existing.Name != Ns + "StructuredText")
        {
            existingUnit.ReplaceWith(generatedUnit);
            return;
        }
        var oldBlocks = Split(existing.Elements().ToList());
        var generated = plan.Regions.ToDictionary(r => r.Name);
        bool Selected(string name) => plan.RegionAssets.TryGetValue(name, out var assets) && assets.Overlaps(selectedAssets);

        var output = new List<Block>();
        foreach (var block in oldBlocks)
        {
            if (block.Name is null || !generated.TryGetValue(block.Name, out var region))
            {
                output.Add(block);
            }
            else if (region.Children.Count == 0)
            {
                output.Add(Selected(region.Name) ? Render(region, 0) : block);
            }
            else
            {
                output.Add(MergeCell(block, region, Selected));
            }
        }
        // Regiones nuevas detrás de la región de la app que las precede.
        string? previous = null;
        foreach (var region in plan.Regions)
        {
            var index = output.FindIndex(b => b.Name == region.Name);
            if (index < 0)
            {
                var at = previous is null ? 0 : output.FindIndex(b => b.Name == previous) + 1;
                output.Insert(at, Render(region, 0));
            }
            previous = region.Name;
        }

        var root = new XElement(Ns + "StructuredText");
        foreach (var block in output)
        {
            root.Add(block.Items.Select(Clone));
            if (block.Name is not null)
            {
                root.Add(NewLine(2));
            }
        }
        Renumber(root);
        existing.ReplaceWith(root);
    }

    private static Block MergeCell(Block old, SclRegion region, Func<string, bool> selected)
    {
        // Contenido de la región: entre la línea REGION nombre y su END_REGION.
        var items = old.Items;
        var headerEnd = items.FindIndex(e => IsNewLine(e));
        var inner = items.Skip(headerEnd + 1).Take(items.Count - headerEnd - 2).ToList();
        var children = Split(inner);
        var generatedChildren = region.Children.ToDictionary(c => c.Name);

        var merged = new List<Block>();
        foreach (var child in children)
        {
            if (child.Name is not null && generatedChildren.TryGetValue(child.Name, out var g))
            {
                merged.Add(selected(g.Name) ? Render(g, 1) : child);
            }
            else if (child.Name is not null || child.Items.Any(e => !IsSpace(e)))
            {
                merged.Add(child);
            }
        }
        string? previous = null;
        foreach (var child in region.Children)
        {
            if (merged.FindIndex(b => b.Name == child.Name) < 0)
            {
                var at = previous is null ? 0 : merged.FindIndex(b => b.Name == previous) + 1;
                merged.Insert(at, Render(child, 1));
            }
            previous = child.Name;
        }

        var result = new List<XElement> { Token("REGION"), Blank(1), Text(region.Name), NewLine(1) };
        for (var i = 0; i < merged.Count; i++)
        {
            if (i > 0)
            {
                result.Add(NewLine(1));
            }
            result.Add(Blank(4));
            result.AddRange(merged[i].Items);
            result.Add(NewLine(1));
        }
        result.Add(Token("END_REGION"));
        return new Block(region.Name, result);
    }

    // Región generada como lista de elementos, sin la sangría inicial ni el salto final.
    private static Block Render(SclRegion region, int depth)
    {
        var writer = new SclWriter();
        writer.Region(region, depth);
        var items = writer.Root.Elements().ToList();
        while (items.Count > 0 && IsSpace(items[0]))
        {
            items.RemoveAt(0);
        }
        while (items.Count > 0 && IsSpace(items[^1]))
        {
            items.RemoveAt(items.Count - 1);
        }
        return new Block(region.Name, items);
    }

    // Parte una secuencia de elementos SCL en regiones de primer nivel y contenido suelto.
    private static List<Block> Split(List<XElement> items)
    {
        var blocks = new List<Block>();
        var loose = new List<XElement>();
        for (var i = 0; i < items.Count; i++)
        {
            if (!IsToken(items[i], "REGION"))
            {
                loose.Add(items[i]);
                continue;
            }
            var end = MatchingEnd(items, i);
            if (loose.Any(e => !IsSpace(e)))
            {
                blocks.Add(new Block(null, Trim(loose)));
            }
            loose = [];
            var region = items.GetRange(i, end - i + 1);
            blocks.Add(new Block(RegionName(region), region));
            i = end;
        }
        if (loose.Any(e => !IsSpace(e)))
        {
            blocks.Add(new Block(null, Trim(loose)));
        }
        return blocks;
    }

    private static int MatchingEnd(List<XElement> items, int start)
    {
        var depth = 0;
        for (var i = start; i < items.Count; i++)
        {
            if (IsToken(items[i], "REGION"))
            {
                depth++;
            }
            else if (IsToken(items[i], "END_REGION") && --depth == 0)
            {
                return i;
            }
        }
        return items.Count - 1;
    }

    // El nombre es el texto que sigue a REGION en la misma línea.
    private static string RegionName(List<XElement> region) =>
        region.Skip(1).TakeWhile(e => !IsNewLine(e)).Where(e => e.Name == Ns + "Text").Select(e => e.Value.Trim()).FirstOrDefault() ?? "";

    private static List<XElement> Trim(List<XElement> items)
    {
        var list = items.SkipWhile(IsSpace).ToList();
        while (list.Count > 0 && IsSpace(list[^1]))
        {
            list.RemoveAt(list.Count - 1);
        }
        return list;
    }

    private static bool IsToken(XElement e, string text) => e.Name == Ns + "Token" && (string?)e.Attribute("Text") == text;

    private static bool IsNewLine(XElement e) => e.Name == Ns + "NewLine";

    private static bool IsSpace(XElement e) => e.Name == Ns + "NewLine" || e.Name == Ns + "Blank";

    private static XElement Token(string text) => new(Ns + "Token", new XAttribute("Text", text));

    private static XElement Blank(int count) => new(Ns + "Blank", new XAttribute("Num", count));

    private static XElement NewLine(int count) => new(Ns + "NewLine", new XAttribute("Num", count));

    private static XElement Text(string text) => new(Ns + "Text", text);

    private static XElement Clone(XElement e) => new(e);

    // Los UId de un segmento SCL no se referencian entre sí: se renumeran desde 21 para que no se repitan.
    private static void Renumber(XElement root)
    {
        var next = 21;
        foreach (var e in root.Descendants())
        {
            if (e.Attribute("UId") is not null || e.Name.LocalName is "Token" or "Blank" or "NewLine" or "Text" or "Access" or "Symbol" or "Component")
            {
                e.SetAttributeValue("UId", next++);
            }
        }
    }
}
