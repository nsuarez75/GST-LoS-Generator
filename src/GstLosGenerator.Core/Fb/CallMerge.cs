// Regenera la llamada de un asset sin tocar la lógica que se le haya cableado a mano.
// Solo se reescribe lo de la app: título, FB llamado, instancia, pin de operación y pin del UDT.
using System.Xml.Linq;

namespace GstLosGenerator.Core.Fb;

public static class CallMerge
{
    private static readonly XNamespace Ns = FbXml.FlgNet;
    private static readonly HashSet<string> PowerPins = ["en", "eno"];

    // Devuelve los pines perdidos si el FB nuevo no los tiene y no se ha confirmado; en ese caso no cambia nada.
    public static PinLoss? Update(XElement unit, CallInfo call, bool confirmed)
    {
        var net = FbXml.Source(unit);
        var parts = net?.Element(Ns + "Parts");
        var wires = net?.Element(Ns + "Wires");
        var callPart = parts?.Elements(Ns + "Call").FirstOrDefault(c => (string?)c.Element(Ns + "CallInfo")?.Attribute("BlockType") == "FB");
        if (net is null || parts is null || wires is null || callPart is null)
        {
            return null;
        }
        var callId = (string)callPart.Attribute("UId")!;
        var info = callPart.Element(Ns + "CallInfo")!;
        var newParameters = Segments.CallParameters(call);

        var oldAssetAccess = FindAccess(parts, call.Element);
        var oldUdtWire = oldAssetAccess is null ? null : WireBetween(wires, (string)oldAssetAccess.Attribute("UId")!, callId);
        var oldUdtPin = oldUdtWire is null ? null : (string?)PinCon(oldUdtWire, callId)?.Attribute("Name");
        var oldOperationWire = OperationWire(parts, wires, callId);
        var oldOperationPin = oldOperationWire is null ? null : (string?)PinCon(oldOperationWire, callId)?.Attribute("Name");

        // Los pines que reescribe la app no cuentan como perdidos.
        if (call.Parameters.Count > 0)
        {
            var available = newParameters.Select(p => p.Name).ToHashSet();
            var lost = wires.Elements(Ns + "Wire")
                .SelectMany(w => w.Elements(Ns + "NameCon"))
                .Where(n => (string?)n.Attribute("UId") == callId)
                .Select(n => (string)n.Attribute("Name")!)
                .Where(p => !PowerPins.Contains(p) && p != oldUdtPin && p != oldOperationPin && !available.Contains(p))
                .Distinct()
                .ToList();
            if (lost.Count > 0 && !confirmed)
            {
                return new PinLoss(call.Element, call.Fb, lost);
            }
            foreach (var pin in lost)
            {
                RemovePin(wires, callId, pin);
            }
        }

        FbXml.SetTitle(unit, call.Title);
        info.SetAttributeValue("Name", call.Fb);
        info.Element(Ns + "Instance")?.Element(Ns + "Component")?.SetAttributeValue("Name", call.Instance);
        info.Elements(Ns + "Parameter").Remove();
        info.Add(newParameters.Select(p => new XElement(Ns + "Parameter",
            new XAttribute("Name", p.Name), new XAttribute("Section", p.Section), new XAttribute("Type", p.Type))));

        var ids = new FreeIds(net);
        // Pin del UDT: se reaprovecha el acceso a la variable del asset si ya estaba.
        if (oldUdtWire is not null)
        {
            oldUdtWire.Remove();
        }
        var assetAccess = oldAssetAccess;
        if (assetAccess is null)
        {
            assetAccess = Segments.LadAccess(ids.Next(), "LocalVariable", [call.Element]);
            parts.AddFirst(assetAccess);
        }
        var assetId = (string)assetAccess.Attribute("UId")!;
        wires.Add(call.UdtPinSection == "Input"
            ? new XElement(Ns + "Wire", new XAttribute("UId", ids.Next()), Segments.IdentCon(assetId), Segments.NameCon(callId, call.UdtPin))
            : new XElement(Ns + "Wire", new XAttribute("UId", ids.Next()), Segments.NameCon(callId, call.UdtPin), Segments.IdentCon(assetId)));

        // Pin de operación: se conecta a la operación asignada o se desconecta si ya no tiene.
        if (oldOperationWire is not null)
        {
            var access = OperationAccess(parts, oldOperationWire);
            oldOperationWire.Remove();
            access?.Remove();
        }
        if (call.OperationVariable is not null && call.OperationPin.Length > 0)
        {
            var access = Segments.LadAccess(ids.Next(), "LocalVariable", [call.OperationVariable]);
            parts.AddFirst(access);
            wires.Add(new XElement(Ns + "Wire", new XAttribute("UId", ids.Next()),
                Segments.IdentCon((string)access.Attribute("UId")!), Segments.NameCon(callId, call.OperationPin)));
        }
        return null;
    }

    private static XElement? FindAccess(XElement parts, string variable) =>
        parts.Elements(Ns + "Access").FirstOrDefault(a => (string?)a.Attribute("Scope") == "LocalVariable"
            && a.Element(Ns + "Symbol")?.Elements(Ns + "Component").Select(c => (string?)c.Attribute("Name")).SequenceEqual([variable]) == true);

    private static XElement? WireBetween(XElement wires, string accessId, string callId) =>
        wires.Elements(Ns + "Wire").FirstOrDefault(w =>
            w.Elements(Ns + "IdentCon").Any(i => (string?)i.Attribute("UId") == accessId)
            && w.Elements(Ns + "NameCon").Any(n => (string?)n.Attribute("UId") == callId));

    private static XElement? PinCon(XElement wire, string callId) =>
        wire.Elements(Ns + "NameCon").FirstOrDefault(n => (string?)n.Attribute("UId") == callId);

    // Cable que lleva una variable Operation_x a la llamada.
    private static XElement? OperationWire(XElement parts, XElement wires, string callId) =>
        wires.Elements(Ns + "Wire").FirstOrDefault(w => PinCon(w, callId) is not null && OperationAccess(parts, w) is not null);

    private static XElement? OperationAccess(XElement parts, XElement wire)
    {
        var id = (string?)wire.Element(Ns + "IdentCon")?.Attribute("UId");
        return id is null ? null : parts.Elements(Ns + "Access").FirstOrDefault(a => (string?)a.Attribute("UId") == id
            && ((string?)a.Element(Ns + "Symbol")?.Element(Ns + "Component")?.Attribute("Name"))?.StartsWith("Operation_", StringComparison.Ordinal) == true
            && a.Element(Ns + "Symbol")!.Elements(Ns + "Component").Count() == 1);
    }

    // Quita la conexión a un pin; si el cable se queda sin destino, se quita entero.
    private static void RemovePin(XElement wires, string callId, string pin)
    {
        foreach (var wire in wires.Elements(Ns + "Wire").ToList())
        {
            var con = wire.Elements(Ns + "NameCon").FirstOrDefault(n => (string?)n.Attribute("UId") == callId && (string?)n.Attribute("Name") == pin);
            if (con is null)
            {
                continue;
            }
            con.Remove();
            if (wire.Elements().Count() < 2)
            {
                wire.Remove();
            }
        }
    }

    // UId libres: por encima del mayor que ya use el segmento.
    private sealed class FreeIds(XElement net)
    {
        private int _next = net.Descendants().Select(e => int.TryParse((string?)e.Attribute("UId"), out var v) ? v : 0).DefaultIfEmpty(20).Max() + 1;

        public string Next() => (_next++).ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
