// Traducciones al inglés de los textos de la interfaz (ver L.cs).
namespace GstLosGenerator.Core;

public static partial class L
{
    private static readonly Dictionary<string, string> En = new()
    {
        // ---- LoS
        ["La LoS no tiene la hoja «{0}»."] = "The LoS has no «{0}» sheet.",
        ["A la hoja «{0}» le falta la columna «{1}»."] = "The «{0}» sheet is missing the «{1}» column.",
        ["PLC nuevo: {0}"] = "New PLC: {0}",
        ["Asset nuevo: {0}"] = "New asset: {0}",
        ["Ya no está en la LoS (se marca, no se borra): {0}"] = "No longer in the LoS (flagged, not deleted): {0}",
        ["Vuelve a estar en la LoS: {0}"] = "Back in the LoS: {0}",
        ["Asset manual que ahora trae la LoS (pasa a ser de la LoS): {0}"] = "Manual asset now present in the LoS (becomes a LoS asset): {0}",
        ["Cambio de tipo en {0}: {1}"] = "Type change in {0}: {1}",
        ["Variables cambiadas en {0}: {1}"] = "Variables changed in {0}: {1}",
        ["Devices de {0}: {1}"] = "Devices of {0}: {1}",
        ["Tipo nuevo en el catálogo, pendiente de equivalencia: {0}"] = "New type in the catalog, equivalence pending: {0}",
        ["Variable que el tipo {0} no tiene: {1}"] = "Variable the type {0} does not have: {1}",
        ["nuevas {0}"] = "added {0}",
        ["quitadas {0}"] = "removed {0}",
        ["cambiadas {0}"] = "changed {0}",

        // ---- archivos
        ["El archivo está vacío."] = "The file is empty.",
        ["{0} no tiene un formato válido: {1}"] = "{0} does not have a valid format: {1}",
    };
}
