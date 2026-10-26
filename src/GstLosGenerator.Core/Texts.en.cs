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

        // ---- validaciones
        ["Dirección repetida en {0}: {1}"] = "Duplicate address in {0}: {1}",
        ["La dirección {0} la usan varias variables: {1}"] = "Address {0} is used by several variables: {1}",
        ["La operación {0} no está asignada a ningún asset."] = "Operation {0} is not assigned to any asset.",
        ["La operación {0} no tiene locations."] = "Operation {0} has no locations.",
        ["La operación {0} usa la location «{1}», que no existe."] = "Operation {0} uses location «{1}», which does not exist.",
        ["{0}: el tipo {1} no tiene equivalencia; no se generará."] = "{0}: type {1} has no equivalence; it will not be generated.",

        // ---- generar y leer el FB
        ["El FB llama a {0}, que no está en el proyecto; no se crea."] = "The FB calls {0}, which is not in the project; it is not created.",
        ["El FB no tiene interfaz."] = "The FB has no interface.",
        ["El FB no tiene segmentos."] = "The FB has no networks.",
        ["El XML no contiene un FB exportado de TIA Portal."] = "The XML does not contain an FB exported from TIA Portal.",
        ["El archivo no es un XML válido: {0}"] = "The file is not valid XML: {0}",
        ["El segmento «{0}» es de un asset que ya no se genera; se deja como está."] = "Network «{0}» belongs to an asset that is no longer generated; it is left as it is.",
        ["Location nueva: {0} ({1})"] = "New location: {0} ({1})",
        ["Location {0}: número {1} → {2}"] = "Location {0}: number {1} → {2}",
        ["Operación nueva: {0}"] = "New operation: {0}",
        ["Operación {0}: locations, referencias o señales distintas"] = "Operation {0}: different locations, references or signals",
        ["Tipo {0}: equivalencia aprendida del FB (FB {1}, UDT {2}, pin {3})"] = "Type {0}: equivalence learned from the FB (FB {1}, UDT {2}, pin {3})",
        ["{0}.{1}: la dirección {2} no está en ningún device; no se escribe en Struct TO DEVICE."] = "{0}.{1}: address {2} is not in any device; it is not written in Struct TO DEVICE.",
        ["{0}: el FB {1} no tiene los pines {2}, conectados a mano; no se regenera hasta que lo confirmes."] = "{0}: FB {1} lacks the pins {2}, wired by hand; it is not regenerated until you confirm.",
        ["{0}: el tipo {1} no tiene equivalencia; no se genera."] = "{0}: type {1} has no equivalence; it is not generated.",
        ["{0}: la operación {1} no existe; se llama sin operación."] = "{0}: operation {1} does not exist; it is called without an operation.",
        ["{0}: llama a FB {1} con UDT {2}, distinto de su tipo"] = "{0}: calls FB {1} with UDT {2}, different from its type",
        ["{0}: no se ve a qué pin va su variable; no se toca su equivalencia."] = "{0}: cannot tell which pin its variable goes to; its equivalence is left untouched.",
        ["{0}: operación {1} → {2}"] = "{0}: operation {1} → {2}",
        ["{0}: su tipo {1} no está en el catálogo."] = "{0}: its type {1} is not in the catalog.",

        // ---- archivos
        ["El archivo está vacío."] = "The file is empty.",
        ["{0} no tiene un formato válido: {1}"] = "{0} does not have a valid format: {1}",
    };
}
