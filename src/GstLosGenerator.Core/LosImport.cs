// Aplica una LoS a un proyecto, nuevo o ya trabajado. Primero se calcula un plan con todos los cambios
// para enseñárselo al usuario; solo al aceptarlo se aplica. Lo hecho a mano se conserva:
// operaciones, tipos cambiados, assets manuales y, si el usuario quiere, variables movidas.
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

// Variable que la LoS sigue poniendo en su asset de origen y que se había movido a otro.
public sealed class MovedVariable(string plc, VariableMove move)
{
    public string Plc { get; } = plc;
    public VariableMove Move { get; } = move;
    // Mantenerla donde se movió; si no, manda la LoS y se olvida el movimiento.
    public bool Keep { get; set; } = true;

    public string Text => L.T("{0}: la LoS pone la variable {1} en {2}, pero la moviste a {3}.", Plc, Move.Name, Move.From, Move.To);
}

public sealed class LosImportPlan
{
    private readonly Project _project;
    private readonly Catalog _catalog;
    private readonly LosData _data;

    public List<Change> Changes { get; } = [];
    // Variables movidas a mano que la LoS trae otra vez en su origen: se pregunta por cada una.
    public List<MovedVariable> MovedVariables { get; } = [];
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
        plan.FindMovedVariables();
        plan.Compute();
        return plan;
    }

    // Vuelve a calcular los cambios tras decidir qué variables movidas se mantienen.
    public void Refresh()
    {
        Changes.Clear();
        Compute();
    }

    private void FindMovedVariables()
    {
        foreach (var losPlc in _data.Plcs)
        {
            if (_project.FindPlc(losPlc.Name) is not { } plc)
            {
                continue;
            }
            foreach (var move in plc.VariableMoves)
            {
                if (losPlc.Assets.FirstOrDefault(a => a.Element == move.From) is { } origin && origin.Variables.Any(v => v.Name == move.Name))
                {
                    MovedVariables.Add(new MovedVariable(plc.Name, move));
                }
            }
        }
    }

    // Movimientos que siguen valiendo: los que el usuario mantiene y los de un origen que la LoS no trae
    // (asset manual). Si la LoS trae el origen sin la variable, el movimiento ya no hace falta.
    private List<VariableMove> KeptMoves(Plc plc, LosPlc losPlc) =>
        plc.VariableMoves.Where(m => MovedVariables.FirstOrDefault(q => q.Move == m) is { } question
            ? question.Keep
            : losPlc.Assets.All(a => a.Element != m.From)).ToList();

    // Variables de cada asset de la LoS con los movimientos que se mantienen ya aplicados.
    private Dictionary<string, List<AssetVariable>> LosVariables(Plc? plc, LosPlc losPlc)
    {
        var result = losPlc.Assets.ToDictionary(a => a.Element, a => a.Variables.ToList());
        if (plc is null)
        {
            return result;
        }
        foreach (var move in KeptMoves(plc, losPlc))
        {
            var origin = result.GetValueOrDefault(move.From);
            var fromLos = origin?.FirstOrDefault(v => v.Name == move.Name);
            if (!result.TryGetValue(move.To, out var to))
            {
                // El destino no es de la LoS (asset manual): ya tiene la variable, solo se quita del origen.
                if (fromLos is not null)
                {
                    origin!.Remove(fromLos);
                }
                continue;
            }
            var variable = fromLos ?? plc.FindAsset(move.To)?.Variables.FirstOrDefault(v => v.Name == move.Name);
            if (variable is null || to.Any(v => v.Name == move.Name))
            {
                continue;
            }
            origin?.Remove(variable);
            to.Add(variable);
        }
        return result;
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
            var variables = LosVariables(plc, losPlc);
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
                var diff = VariableDiff(existing.Variables, variables[asset.Element]);
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
            var kept = KeptMoves(plc, losPlc);
            var variables = LosVariables(plc, losPlc);
            ApplyAssets(plc, losPlc, variables);
            plc.VariableMoves = kept;
        }
        ApplyCatalog();
    }

    private static void ApplyAssets(Plc plc, LosPlc losPlc, Dictionary<string, List<AssetVariable>> variables)
    {
        var inLos = losPlc.Assets.Select(a => a.Element).ToHashSet();
        var result = new List<Asset>();
        foreach (var asset in losPlc.Assets)
        {
            asset.Variables = variables[asset.Element];
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
            // Las variables desactivadas siguen desactivadas si la LoS las conserva.
            foreach (var variable in asset.Variables)
            {
                variable.Disabled = existing.Variables.Any(v => v.Name == variable.Name && v.Disabled);
            }
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
