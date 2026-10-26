// Aplica una LoS a un proyecto, nuevo o ya trabajado. Primero se calcula un plan con todos los cambios
// para enseñárselo al usuario; solo al aceptarlo se aplica. Lo hecho a mano se conserva:
// operaciones, tipos cambiados, assets manuales.
namespace GstLosGenerator.Core;

public enum ChangeKind
{
    PlcAdded,
    AssetAdded,
    AssetMissing,
    AssetBack,
    AssetAdopted,
    AssetTypeChanged,
    VariablesChanged,
    DevicesChanged,
    TypeAdded,
    TypeVariableAdded,
}

public sealed record Change(ChangeKind Kind, string Plc, string Subject, string Detail)
{
    public string Text => Kind switch
    {
        ChangeKind.PlcAdded => L.T("PLC nuevo: {0}", Subject),
        ChangeKind.AssetAdded => L.T("Asset nuevo: {0}", Subject),
        ChangeKind.AssetMissing => L.T("Ya no está en la LoS (se marca, no se borra): {0}", Subject),
        ChangeKind.AssetBack => L.T("Vuelve a estar en la LoS: {0}", Subject),
        ChangeKind.AssetAdopted => L.T("Asset manual que ahora trae la LoS (pasa a ser de la LoS): {0}", Subject),
        ChangeKind.AssetTypeChanged => L.T("Cambio de tipo en {0}: {1}", Subject, Detail),
        ChangeKind.VariablesChanged => L.T("Variables cambiadas en {0}: {1}", Subject, Detail),
        ChangeKind.DevicesChanged => L.T("Devices de {0}: {1}", Subject, Detail),
        ChangeKind.TypeAdded => L.T("Tipo nuevo en el catálogo, pendiente de equivalencia: {0}", Subject),
        ChangeKind.TypeVariableAdded => L.T("Variable que el tipo {0} no tiene: {1}", Subject, Detail),
        _ => Subject,
    };
}

public sealed class LosImportPlan
{
    private readonly Project _project;
    private readonly Catalog _catalog;
    private readonly LosData _data;

    public List<Change> Changes { get; } = [];
    // Añadir al tipo las variables que traen los assets y el tipo no tiene.
    public bool AddVariablesToTypes { get; set; } = true;

    public bool HasChanges => Changes.Count > 0;

    private LosImportPlan(Project project, Catalog catalog, LosData data)
    {
        _project = project;
        _catalog = catalog;
        _data = data;
    }

    public static LosImportPlan Create(Project project, Catalog catalog, LosData data)
    {
        var plan = new LosImportPlan(project, catalog, data);
        plan.Compute();
        return plan;
    }

    private void Compute()
    {
        foreach (var losPlc in _data.Plcs)
        {
            var plc = _project.FindPlc(losPlc.Name);
            if (plc is null)
            {
                Changes.Add(new(ChangeKind.PlcAdded, losPlc.Name, losPlc.Name, ""));
                foreach (var asset in losPlc.Assets)
                {
                    Changes.Add(new(ChangeKind.AssetAdded, losPlc.Name, asset.Element, ""));
                }
                continue;
            }
            CompareDevices(plc, losPlc);
            var inLos = losPlc.Assets.Select(a => a.Element).ToHashSet();
            foreach (var asset in losPlc.Assets)
            {
                var existing = plc.FindAsset(asset.Element);
                if (existing is null)
                {
                    Changes.Add(new(ChangeKind.AssetAdded, plc.Name, asset.Element, ""));
                    continue;
                }
                if (existing.Manual)
                {
                    Changes.Add(new(ChangeKind.AssetAdopted, plc.Name, asset.Element, ""));
                }
                else if (existing.Missing)
                {
                    Changes.Add(new(ChangeKind.AssetBack, plc.Name, asset.Element, ""));
                }
                if (existing.LosType != asset.Type && existing.Type == existing.LosType)
                {
                    Changes.Add(new(ChangeKind.AssetTypeChanged, plc.Name, asset.Element, $"{existing.Type} → {asset.Type}"));
                }
                var diff = VariableDiff(existing.Variables, asset.Variables);
                if (diff.Length > 0)
                {
                    Changes.Add(new(ChangeKind.VariablesChanged, plc.Name, asset.Element, diff));
                }
            }
            foreach (var asset in plc.Assets.Where(a => !a.Manual && !a.Missing && !inLos.Contains(a.Element)))
            {
                Changes.Add(new(ChangeKind.AssetMissing, plc.Name, asset.Element, ""));
            }
        }
        CompareCatalog();
    }

    private void CompareDevices(Plc plc, LosPlc losPlc)
    {
        var before = string.Join(", ", plc.Devices.Select(d => $"{plc.DeviceName(d.Number)} ({d.Addresses.Count})"));
        var after = string.Join(", ", losPlc.Devices.Select(d => $"{Naming.DeviceName(losPlc.Number, d.Number)} ({d.Addresses.Count})"));
        var same = plc.Devices.Count == losPlc.Devices.Count
            && plc.Devices.Zip(losPlc.Devices).All(p => p.First.Number == p.Second.Number
                && p.First.Addresses.Select(a => a.Name).SequenceEqual(p.Second.Addresses.Select(a => a.Name)));
        if (!same)
        {
            Changes.Add(new(ChangeKind.DevicesChanged, plc.Name, plc.Name, $"{before} → {after}"));
        }
    }

    private static string VariableDiff(List<AssetVariable> before, List<AssetVariable> after)
    {
        var parts = new List<string>();
        var old = before.ToDictionary(v => v.Name + "|" + v.Direction);
        var now = after.ToDictionary(v => v.Name + "|" + v.Direction);
        var added = now.Keys.Except(old.Keys).Select(k => now[k].Name).ToList();
        var removed = old.Keys.Except(now.Keys).Select(k => old[k].Name).ToList();
        var changed = now.Keys.Intersect(old.Keys)
            .Where(k => now[k].Address != old[k].Address || now[k].DataType != old[k].DataType)
            .Select(k => $"{now[k].Name} ({old[k].Address} → {now[k].Address})").ToList();
        if (added.Count > 0)
        {
            parts.Add(L.T("nuevas {0}", string.Join(", ", added)));
        }
        if (removed.Count > 0)
        {
            parts.Add(L.T("quitadas {0}", string.Join(", ", removed)));
        }
        if (changed.Count > 0)
        {
            parts.Add(L.T("cambiadas {0}", string.Join(", ", changed)));
        }
        return string.Join("; ", parts);
    }

    private void CompareCatalog()
    {
        var newTypes = new Dictionary<string, AssetType>();
        foreach (var asset in _data.Plcs.SelectMany(p => p.Assets))
        {
            if (asset.Type.Length == 0)
            {
                continue;
            }
            var type = _catalog.Find(asset.Type);
            if (type is null)
            {
                if (!newTypes.ContainsKey(asset.Type))
                {
                    newTypes[asset.Type] = new AssetType { Name = asset.Type };
                    Changes.Add(new(ChangeKind.TypeAdded, "", asset.Type, ""));
                }
                continue;
            }
            foreach (var variable in asset.Variables)
            {
                if (!type.Variables.Any(v => v.Name == variable.Name)
                    && !Changes.Any(c => c.Kind == ChangeKind.TypeVariableAdded && c.Subject == type.Name && c.Detail == variable.Name))
                {
                    Changes.Add(new(ChangeKind.TypeVariableAdded, "", type.Name, variable.Name));
                }
            }
        }
    }

    public void Apply()
    {
        foreach (var losPlc in _data.Plcs)
        {
            var plc = _project.FindPlc(losPlc.Name);
            if (plc is null)
            {
                plc = new Plc { Name = losPlc.Name, Number = losPlc.Number };
                _project.Plcs.Add(plc);
            }
            plc.Devices = losPlc.Devices;
            ApplyAssets(plc, losPlc);
        }
        ApplyCatalog();
    }

    private static void ApplyAssets(Plc plc, LosPlc losPlc)
    {
        var inLos = losPlc.Assets.Select(a => a.Element).ToHashSet();
        var result = new List<Asset>();
        foreach (var asset in losPlc.Assets)
        {
            var existing = plc.FindAsset(asset.Element);
            if (existing is null)
            {
                asset.LosType = asset.Type;
                result.Add(asset);
                continue;
            }
            // El tipo de la LoS manda salvo que el usuario lo hubiera cambiado a mano.
            if (existing.Manual || existing.Type == existing.LosType)
            {
                existing.Type = asset.Type;
            }
            existing.LosType = asset.Type;
            existing.Kind = asset.Kind;
            existing.LayoutName = asset.LayoutName;
            existing.Variables = asset.Variables;
            existing.Manual = false;
            existing.Missing = false;
            result.Add(existing);
        }
        // Los que ya no están en la LoS se quedan, marcados, cerca de donde estaban.
        for (var i = 0; i < plc.Assets.Count; i++)
        {
            var asset = plc.Assets[i];
            if (inLos.Contains(asset.Element))
            {
                continue;
            }
            if (!asset.Manual)
            {
                asset.Missing = true;
            }
            var previous = plc.Assets.Take(i).LastOrDefault(a => result.Contains(a));
            var at = previous is null ? 0 : result.IndexOf(previous) + 1;
            result.Insert(at, asset);
        }
        plc.Assets = result;
    }

    private void ApplyCatalog()
    {
        foreach (var asset in _data.Plcs.SelectMany(p => p.Assets))
        {
            if (asset.Type.Length == 0)
            {
                continue;
            }
            var type = _catalog.Find(asset.Type);
            if (type is null)
            {
                type = new AssetType { Name = asset.Type };
                _catalog.Types.Add(type);
                AddVariables(type, asset);
            }
            else if (AddVariablesToTypes)
            {
                AddVariables(type, asset);
            }
        }
    }

    private static void AddVariables(AssetType type, Asset asset)
    {
        foreach (var variable in asset.Variables)
        {
            if (!type.Variables.Any(v => v.Name == variable.Name))
            {
                type.Variables.Add(new TypeVariable { Name = variable.Name, DataType = variable.DataType });
            }
        }
    }
}
