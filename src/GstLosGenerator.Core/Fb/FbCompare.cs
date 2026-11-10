// ¿Es el mismo FB? Antes de subir a TIA se compara el FB que hay en TIA con el último leído: si alguien lo ha
// cambiado a mano desde entonces, no se sube para no perder ese trabajo. No cuentan las fechas de compilación y
// modificación, el formato ni la cabecera de la exportación (versión, productos instalados y la hora a la que se
// exportó, que cambia en cada exportación).
using System.Xml;
using System.Xml.Linq;

namespace GstLosGenerator.Core.Fb;

public static class FbCompare
{
    public static bool Same(string a, string b)
    {
        try
        {
            return XNode.DeepEquals(Normalize(a), Normalize(b));
        }
        catch (XmlException)
        {
            return false;
        }
    }

    private static XElement Normalize(string xml)
    {
        var root = XDocument.Parse(xml, LoadOptions.None).Root ?? throw new XmlException("XML vacío.");
        root.DescendantsAndSelf()
            .Where(e => e.Name.LocalName.EndsWith("Date", StringComparison.Ordinal) || e.Name.LocalName is "Engineering" or "DocumentInfo")
            .ToList()
            .ForEach(e => e.Remove());
        return root;
    }
}
