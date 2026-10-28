// Mover variables de un asset a otro (arrastrándolas en el árbol). El movimiento se apunta en el PLC
// para volver a aplicarlo cuando una LoS nueva traiga la variable otra vez en su asset de origen.
namespace GstLosGenerator.Core;

public static class VariableMover
{
    // Motivo por el que no se puede mover; null si se puede.
    public static string? CannotMove(Asset to, AssetVariable variable) =>
        to.Variables.Any(v => v.Name == variable.Name)
            ? L.T("{0} ya tiene una variable {1}; no se mueve.", to.Element, variable.Name)
            : null;

    // Mueve la variable y devuelve los avisos (el tipo de destino no la tenía).
    public static List<string> Move(Plc plc, Catalog catalog, Asset from, Asset to, AssetVariable variable)
    {
        var warnings = new List<string>();
        from.Variables.Remove(variable);
        to.Variables.Add(variable);

        // Si ya venía movida, se apunta solo el origen de la LoS y el destino final.
        var earlier = plc.VariableMoves.FirstOrDefault(m => m.To == from.Element && m.Name == variable.Name);
        if (earlier is null)
        {
            plc.VariableMoves.Add(new VariableMove { From = from.Element, To = to.Element, Name = variable.Name });
        }
        else if (earlier.From == to.Element)
        {
            plc.VariableMoves.Remove(earlier);
        }
        else
        {
            earlier.To = to.Element;
        }

        if (catalog.Find(to.Type) is { } type && !type.Variables.Any(v => v.Name == variable.Name))
        {
            type.Variables.Add(new TypeVariable { Name = variable.Name, DataType = variable.DataType });
            warnings.Add(L.T("El tipo {0} no tenía la variable {1}; se añade al catálogo. Revisa que su UDT la tenga en TIA.", type.Name, variable.Name));
        }
        return warnings;
    }
}
