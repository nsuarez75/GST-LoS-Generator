// Comprobaciones de un PLC antes de generar. Avisan, no impiden generar.
namespace GstLosGenerator.Core;

public enum IssueTarget
{
    Operation,
    Asset,
    Device,
    Type,
}

// Aviso con lo que lo provoca, para poder ir a ello desde la barra de avisos.
public sealed record Issue(string Plc, IssueTarget Target, string Subject, string Text);

public static class Validation
{
    public static List<Issue> Check(Project project, Catalog catalog) =>
        project.Plcs.SelectMany(p => Check(p, catalog)).ToList();

    public static List<Issue> Check(Plc plc, Catalog catalog)
    {
        var issues = new List<Issue>();
        foreach (var operation in plc.Operations)
        {
            if (operation.Locations.Count == 0)
            {
                issues.Add(new(plc.Name, IssueTarget.Operation, operation.Id, L.T("La operación {0} no tiene locations.", operation.Id)));
            }
            if (!plc.Assets.Any(a => a.Operation == operation.Id))
            {
                issues.Add(new(plc.Name, IssueTarget.Operation, operation.Id, L.T("La operación {0} no está asignada a ningún asset.", operation.Id)));
            }
            foreach (var location in operation.Locations.Where(l => plc.FindLocation(l.Location) is null))
            {
                issues.Add(new(plc.Name, IssueTarget.Operation, operation.Id, L.T("La operación {0} usa la location «{1}», que no existe.", operation.Id, location.Location)));
            }
        }
        foreach (var asset in plc.Assets.Where(a => !a.Missing))
        {
            var type = catalog.Find(asset.Type);
            if (type is null || !type.HasEquivalence)
            {
                issues.Add(new(plc.Name, IssueTarget.Asset, asset.Element, L.T("{0}: el tipo {1} no tiene equivalencia; no se generará.", asset.Element, asset.Type)));
            }
        }
        foreach (var device in plc.Devices)
        {
            foreach (var name in device.Addresses.GroupBy(a => a.Name).Where(g => g.Count() > 1).Select(g => g.Key))
            {
                issues.Add(new(plc.Name, IssueTarget.Device, plc.DeviceName(device.Number), L.T("Dirección repetida en {0}: {1}", plc.DeviceName(device.Number), name)));
            }
        }
        var used = plc.Assets.Where(a => !a.Missing)
            .SelectMany(a => a.Variables.Where(v => v.IsOutput && !v.Disabled && v.Address.Length > 0).Select(v => (Asset: a.Element, v.Address)))
            .GroupBy(x => x.Address)
            .Where(g => g.Count() > 1);
        foreach (var group in used)
        {
            issues.Add(new(plc.Name, IssueTarget.Asset, group.First().Asset,
                L.T("La dirección {0} la usan varias variables: {1}", group.Key, string.Join(", ", group.Select(g => g.Asset)))));
        }
        return issues;
    }
}
