// Lo que se dice con el puente de TIA (GstLosGenerator.Tia, .NET Framework 4.8): órdenes de una línea con los campos
// separados por tabuladores, y respuestas «CLAVE texto», una por línea, que terminan en DONE o ERROR.
namespace GstLosGenerator.Core.Tia;

public sealed class TiaException(string message) : Exception(message);

// Bloque del árbol de un PLC. Group son los nombres de las carpetas separados por «/»; vacío es la raíz.
public sealed record TiaBlock(string Plc, string Group, string Name, string Kind);

public sealed record TiaGroup(string Plc, string Path);

// PLC, carpetas y bloques de un proyecto de TIA.
public sealed class TiaTree
{
    public List<string> Plcs { get; } = [];
    public List<TiaGroup> Groups { get; } = [];
    public List<TiaBlock> Blocks { get; } = [];

    public TiaBlock? FindBlock(string plc, string name) => Blocks.FirstOrDefault(b => b.Plc == plc && b.Name == name);
}

// Lo que pasa al subir: UDT subidos, dónde queda el FB, resultado de compilar y el FB tal como queda en TIA.
public sealed class TiaImportResult
{
    public List<string> Udts { get; } = [];
    public string? Group { get; set; }
    public string? Block { get; set; }
    // Success, Information, Warning o Error; null si no se compiló.
    public string? CompileState { get; set; }
    public int Errors { get; set; }
    public int Warnings { get; set; }
    // Mensajes de error y aviso de la compilación: «Error · ruta: texto».
    public List<string> Messages { get; } = [];
    // Avisos del puente (por ejemplo, que no se pudo volver a exportar el FB).
    public List<string> Notes { get; } = [];
    public string? ExportedFile { get; set; }
    // Su contenido, que lee la app.
    public string? ExportedXml { get; set; }
}

public static class TiaProtocol
{
    public const string Done = "DONE";
    public const string Error = "ERROR";
    public const string Ready = "READY";
    public const string Progress = "PROGRESS";

    public static string Command(params string[] fields)
    {
        foreach (var field in fields)
        {
            if (field.IndexOfAny(['\t', '\r', '\n']) >= 0)
            {
                throw new ArgumentException("Un campo de la orden no puede llevar tabuladores ni saltos de línea: " + field);
            }
        }
        return string.Join('\t', fields);
    }

    public static (string Key, string Text) Split(string line)
    {
        var space = line.IndexOf(' ', StringComparison.Ordinal);
        return space < 0 ? (line, "") : (line[..space], line[(space + 1)..]);
    }

    public static void ApplyTree(TiaTree tree, string key, string text)
    {
        var parts = text.Split('|');
        switch (key)
        {
            case "PLC":
                tree.Plcs.Add(text);
                break;
            case "GROUP" when parts.Length == 2:
                tree.Groups.Add(new TiaGroup(parts[0], parts[1]));
                break;
            case "BLOCK" when parts.Length == 4:
                tree.Blocks.Add(new TiaBlock(parts[0], parts[1], parts[2], parts[3]));
                break;
        }
    }

    public static void ApplyImport(TiaImportResult result, string key, string text)
    {
        var parts = text.Split('|');
        switch (key)
        {
            case "UDT":
                result.Udts.Add(text);
                break;
            case "PLACED" when parts.Length == 2:
                result.Group = parts[0];
                result.Block = parts[1];
                break;
            case "COMPILE" when parts.Length == 3:
                result.CompileState = parts[0];
                result.Errors = int.TryParse(parts[1], out var errors) ? errors : 0;
                result.Warnings = int.TryParse(parts[2], out var warnings) ? warnings : 0;
                break;
            case "MSG" when parts.Length >= 3:
                result.Messages.Add($"{parts[0]} · {parts[1]}: {string.Join('|', parts.Skip(2))}");
                break;
            case "WARN":
                result.Notes.Add(text);
                break;
            case "EXPORTED":
                result.ExportedFile = text;
                break;
        }
    }
}
