// Piezas del XML de Openness (TIA Portal V19) con las que se monta el FB, copiando el formato
// de un FB exportado: segmentos LAD (FlgNet), STL (StatementList) y SCL (StructuredText).
using System.Globalization;
using System.Xml.Linq;

namespace GstLosGenerator.Core.Fb;

public static class FbXml
{
    public static readonly XNamespace Interface = "http://www.siemens.com/automation/Openness/SW/Interface/v5";
    public static readonly XNamespace FlgNet = "http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5";
    public static readonly XNamespace Stl = "http://www.siemens.com/automation/Openness/SW/NetworkSource/StatementList/v5";
    public static readonly XNamespace Scl = "http://www.siemens.com/automation/Openness/SW/NetworkSource/StructuredText/v4";

    public static readonly string[] DefaultCultures = ["es-ES", "en-US", "de-DE"];

    public const string Lad = "LAD";
    public const string StlLanguage = "STL";
    public const string SclLanguage = "SCL";

    // Segmento (CompileUnit). Los ID se renumeran al montar el documento.
    public static XElement CompileUnit(string language, XElement? source, string title, IReadOnlyList<string> cultures) =>
        new("SW.Blocks.CompileUnit",
            new XAttribute("ID", "0"),
            new XAttribute("CompositionName", "CompileUnits"),
            new XElement("AttributeList",
                source is null ? new XElement("NetworkSource") : new XElement("NetworkSource", source),
                new XElement("ProgrammingLanguage", language)),
            new XElement("ObjectList",
                MultilingualText("Comment", "", cultures),
                MultilingualText("Title", title, cultures)));

    public static XElement MultilingualText(string composition, string text, IReadOnlyList<string> cultures) =>
        new("MultilingualText",
            new XAttribute("ID", "0"),
            new XAttribute("CompositionName", composition),
            new XElement("ObjectList",
                cultures.Select(c => new XElement("MultilingualTextItem",
                    new XAttribute("ID", "0"),
                    new XAttribute("CompositionName", "Items"),
                    new XElement("AttributeList",
                        new XElement("Culture", c),
                        new XElement("Text", text))))));

    // Título del segmento: el primer texto no vacío entre sus idiomas.
    public static string Title(XElement unit) =>
        TitleItems(unit).Select(t => t.Value).FirstOrDefault(t => t.Length > 0) ?? "";

    public static void SetTitle(XElement unit, string title)
    {
        foreach (var text in TitleItems(unit))
        {
            text.Value = title;
        }
    }

    private static IEnumerable<XElement> TitleItems(XElement unit) =>
        unit.Element("ObjectList")?.Elements("MultilingualText")
            .Where(m => (string?)m.Attribute("CompositionName") == "Title")
            .SelectMany(m => m.Descendants("Text")) ?? [];

    public static string Language(XElement unit) => unit.Element("AttributeList")?.Element("ProgrammingLanguage")?.Value ?? "";

    public static XElement? Source(XElement unit) => unit.Element("AttributeList")?.Element("NetworkSource")?.Elements().FirstOrDefault();

    // Partes de un nombre de símbolo: "DB".campo o ++1000R1-IN.doPP01 -> componentes separados por puntos,
    // respetando las comillas. Un # delante indica variable local del bloque.
    public static (bool Local, List<string> Components) ParseSymbol(string text)
    {
        var s = text.Trim();
        var local = s.StartsWith('#');
        if (local)
        {
            s = s[1..];
        }
        var parts = new List<string>();
        var current = new System.Text.StringBuilder();
        var quoted = false;
        foreach (var ch in s)
        {
            if (ch == '"')
            {
                quoted = !quoted;
            }
            else if (ch == '.' && !quoted)
            {
                parts.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(ch);
            }
        }
        parts.Add(current.ToString());
        return (local, parts);
    }

    public static string SymbolText(XElement access)
    {
        var symbol = access.Elements().FirstOrDefault(e => e.Name.LocalName == "Symbol");
        if (symbol is null)
        {
            return "";
        }
        var names = symbol.Elements().Where(e => e.Name.LocalName == "Component").Select(c => (string?)c.Attribute("Name") ?? "");
        var text = string.Join(".", names);
        return (string?)access.Attribute("Scope") == "LocalVariable" ? "#" + text : text;
    }

    public static string Constant(long value) => value.ToString(CultureInfo.InvariantCulture);
}

// Contador de UId dentro de un segmento: TIA los numera desde 21.
public sealed class UIds
{
    private int _next = 21;

    public string Next() => (_next++).ToString(CultureInfo.InvariantCulture);
}
