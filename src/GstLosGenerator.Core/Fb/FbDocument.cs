// Un FB en XML de Openness: abrirlo, reconocer sus segmentos, tocar su interfaz y volver a escribirlo.
using System.Globalization;
using System.Text;
using System.Xml.Linq;

namespace GstLosGenerator.Core.Fb;

public static class SegmentKeys
{
    public const string Config = "config";
    public const string OperationsHeader = "ops-header";
    public const string CallsHeader = "calls-header";
    public const string LifeBit = "lifebit";
    public const string Separator1 = "sep1";
    public const string Struct = "struct";
    public const string Separator2 = "sep2";
    public const string SwapFloat = "swapfloat";
    public const string Swap = "swap";

    public static string Operation(string id) => "op:" + id;

    public static string Call(string element) => "call:" + element;
}

public sealed class FbSegment(XElement unit, string? key)
{
    public XElement Unit { get; set; } = unit;
    // Qué segmento de la app es; null si es un segmento hecho a mano.
    public string? Key { get; set; } = key;
    public string Title => FbXml.Title(Unit);
}

public sealed class FbFormatException(string message) : Exception(message);

public sealed class FbDocument
{
    public XDocument Document { get; }
    public XElement Block { get; }
    public List<FbSegment> Segments { get; } = [];
    public IReadOnlyList<string> Cultures { get; }

    private FbDocument(XDocument document)
    {
        Document = document;
        Block = document.Root?.Element("SW.Blocks.FB")
            ?? throw new FbFormatException(L.T("El XML no contiene un FB exportado de TIA Portal."));
        var units = ObjectList.Elements("SW.Blocks.CompileUnit").ToList();
        Cultures = units.SelectMany(u => u.Descendants("Culture")).Select(c => c.Value).Distinct().ToList() is { Count: > 0 } cultures
            ? cultures
            : FbXml.DefaultCultures;
        var used = new HashSet<string>();
        foreach (var unit in units)
        {
            var key = Recognize(unit);
            if (key is not null && !used.Add(key))
            {
                key = null;
            }
            Segments.Add(new FbSegment(unit, key));
        }
    }

    public XElement ObjectList => Block.Element("ObjectList") ?? throw new FbFormatException(L.T("El FB no tiene segmentos."));

    public XElement Sections =>
        Block.Element("AttributeList")?.Element("Interface")?.Element(FbXml.Interface + "Sections")
        ?? throw new FbFormatException(L.T("El FB no tiene interfaz."));

    public XElement Section(string name)
    {
        var section = Sections.Elements(FbXml.Interface + "Section").FirstOrDefault(s => (string?)s.Attribute("Name") == name);
        if (section is null)
        {
            section = new XElement(FbXml.Interface + "Section", new XAttribute("Name", name));
            Sections.Add(section);
        }
        return section;
    }

    public string Name => Block.Element("AttributeList")?.Element("Name")?.Value ?? "";

    public static FbDocument Parse(string xml)
    {
        try
        {
            return new FbDocument(XDocument.Parse(xml, LoadOptions.PreserveWhitespace));
        }
        catch (System.Xml.XmlException e)
        {
            throw new FbFormatException(L.T("El archivo no es un XML válido: {0}", e.Message));
        }
    }

    // FB vacío con los atributos de un FB exportado de V19.
    public static FbDocument CreateEmpty()
    {
        var doc = new XDocument(new XDeclaration("1.0", "utf-8", null),
            new XElement("Document",
                new XElement("Engineering", new XAttribute("version", "V19")),
                new XElement("SW.Blocks.FB", new XAttribute("ID", "0"),
                    new XElement("AttributeList",
                        new XElement("AutoNumber", "true"),
                        new XElement("HeaderAuthor"),
                        new XElement("HeaderFamily"),
                        new XElement("HeaderName"),
                        new XElement("HeaderVersion", "0.1"),
                        new XElement("Interface",
                            new XElement(FbXml.Interface + "Sections",
                                new[] { "Input", "Output", "InOut", "Static", "Temp", "Constant" }
                                    .Select(n => new XElement(FbXml.Interface + "Section", new XAttribute("Name", n))))),
                        new XElement("IsIECCheckEnabled", "false"),
                        new XElement("MemoryLayout", "Standard"),
                        new XElement("Name", Naming.FbName),
                        new XElement("Namespace"),
                        new XElement("ProgrammingLanguage", "LAD"),
                        new XElement("SetENOAutomatically", "false")),
                    new XElement("ObjectList",
                        FbXml.MultilingualText("Comment", "", FbXml.DefaultCultures),
                        FbXml.MultilingualText("Title", "", FbXml.DefaultCultures)))));
        return new FbDocument(doc);
    }

    // Reconoce los segmentos que pone la app por su contenido o su título.
    public static string? Recognize(XElement unit)
    {
        var title = FbXml.Title(unit).Trim();
        var language = FbXml.Language(unit);
        if (language == FbXml.StlLanguage && OperationOf(unit) is { } operation)
        {
            return SegmentKeys.Operation(operation);
        }
        if (language == FbXml.Lad && CallElement(unit) is { } element)
        {
            return SegmentKeys.Call(element);
        }
        if (title.Contains("Config Signals", StringComparison.OrdinalIgnoreCase) || title.Contains("Config Singals", StringComparison.OrdinalIgnoreCase))
        {
            return SegmentKeys.Config;
        }
        // «Setup Operetations» con la errata de algunos FB antiguos.
        if (title.Contains("Setup Oper", StringComparison.OrdinalIgnoreCase))
        {
            return SegmentKeys.OperationsHeader;
        }
        if (title.Contains("Call all blocks", StringComparison.OrdinalIgnoreCase))
        {
            return SegmentKeys.CallsHeader;
        }
        if (title.Equals(Fb.Segments.LifeBitTitle, StringComparison.OrdinalIgnoreCase))
        {
            return SegmentKeys.LifeBit;
        }
        if (title.Length >= 10 && title.All(c => c == '='))
        {
            return SegmentKeys.Separator1;
        }
        if (title.Length >= 10 && title.All(c => c == '*'))
        {
            return SegmentKeys.Separator2;
        }
        if (language == FbXml.SclLanguage && title.StartsWith(Fb.Segments.StructTitle, StringComparison.OrdinalIgnoreCase)
            && FbXml.Source(unit)?.Elements(FbXml.Scl + "Token").Any(t => (string?)t.Attribute("Text") == "REGION") == true)
        {
            return SegmentKeys.Struct;
        }
        if (title.Equals(Fb.Segments.SwapFloatTitle, StringComparison.OrdinalIgnoreCase))
        {
            return SegmentKeys.SwapFloat;
        }
        if (title.Equals(Fb.Segments.SwapTitle, StringComparison.OrdinalIgnoreCase) && language == FbXml.SclLanguage)
        {
            return SegmentKeys.Swap;
        }
        return null;
    }

    // Operación que configura un segmento STL: la del primer «T Operation_x.Amount».
    public static string? OperationOf(XElement unit)
    {
        var list = FbXml.Source(unit);
        if (list is null || list.Name != FbXml.Stl + "StatementList")
        {
            return null;
        }
        foreach (var statement in list.Elements(FbXml.Stl + "StlStatement"))
        {
            if ((string?)statement.Element(FbXml.Stl + "StlToken")?.Attribute("Text") != "T")
            {
                continue;
            }
            var components = statement.Element(FbXml.Stl + "Access")?.Element(FbXml.Stl + "Symbol")?.Elements(FbXml.Stl + "Component").ToList();
            if (components is { Count: 2 } && (string?)components[1].Attribute("Name") == "Amount"
                && ((string?)components[0].Attribute("Name"))?.StartsWith("Operation_", StringComparison.Ordinal) == true)
            {
                return ((string)components[0].Attribute("Name")!)["Operation_".Length..];
            }
        }
        return null;
    }

    // Asset al que llama un segmento LAD: el del título «Element [FB]» si el segmento tiene una llamada a FB.
    public static string? CallElement(XElement unit)
    {
        var source = FbXml.Source(unit);
        if (source is null || !source.Descendants(FbXml.FlgNet + "CallInfo").Any(c => (string?)c.Attribute("BlockType") == "FB"))
        {
            return null;
        }
        var title = FbXml.Title(unit).Trim();
        var bracket = title.IndexOf(" [", StringComparison.Ordinal);
        return bracket > 0 && title.EndsWith(']') ? title[..bracket].Trim() : null;
    }

    public FbSegment? Find(string key) => Segments.FirstOrDefault(s => s.Key == key);

    // Pone los segmentos en el documento en el orden de la lista y renumera los ID.
    public string Write()
    {
        var objectList = ObjectList;
        objectList.Elements("SW.Blocks.CompileUnit").Remove();
        var title = objectList.Elements("MultilingualText").FirstOrDefault(m => (string?)m.Attribute("CompositionName") == "Title");
        foreach (var segment in Segments)
        {
            if (title is null)
            {
                objectList.Add(segment.Unit);
            }
            else
            {
                title.AddBeforeSelf(segment.Unit);
            }
        }
        Renumber();
        var settings = new System.Xml.XmlWriterSettings
        {
            Indent = true,
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true),
            NewLineChars = "\r\n",
        };
        using var stream = new MemoryStream();
        using (var writer = System.Xml.XmlWriter.Create(stream, settings))
        {
            StripWhitespace(Document).Save(writer);
        }
        return new UTF8Encoding(false).GetString(stream.ToArray()).TrimStart('﻿');
    }

    // Los ID de objetos (bloque, textos, segmentos) son hexadecimales y únicos en todo el documento.
    private void Renumber()
    {
        var next = 0;
        foreach (var element in Block.DescendantsAndSelf().Where(e => e.Attribute("ID") is not null))
        {
            element.SetAttributeValue("ID", (next++).ToString("X", CultureInfo.InvariantCulture));
        }
    }

    // Quita los espacios de formato del original para que el XML de salida tenga una sangría uniforme.
    private static XDocument StripWhitespace(XDocument document)
    {
        var copy = new XDocument(document);
        foreach (var text in copy.DescendantNodes().OfType<XText>().Where(t => t is not XCData && string.IsNullOrWhiteSpace(t.Value) && t.Parent?.Nodes().Any(n => n is XElement) == true).ToList())
        {
            text.Remove();
        }
        return copy;
    }
}
