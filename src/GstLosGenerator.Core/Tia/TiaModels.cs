// Lo que la app usa de un proyecto de TIA: el árbol de PLC, carpetas y bloques y el resultado de subir el FB. Se llena
// con lo que devuelve TIA-Connector (ver Services/TiaConnection.cs en la app).
using TiaConnector.Contracts;

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

    // Árbol de la app a partir del de TIA-Connector: cada carpeta con su ruta desde la raíz de bloques del PLC.
    public static TiaTree From(IEnumerable<PlcTree> plcs)
    {
        var tree = new TiaTree();
        foreach (var plc in plcs)
        {
            tree.Plcs.Add(plc.Name);
            tree.Add(plc.Name, plc.Root, "");
        }
        return tree;
    }

    private void Add(string plc, BlockFolder folder, string path)
    {
        foreach (var block in folder.Blocks)
        {
            Blocks.Add(new TiaBlock(plc, path, block.Name, block.Type));
        }
        foreach (var sub in folder.Folders)
        {
            var subPath = path.Length == 0 ? sub.Name : path + "/" + sub.Name;
            Groups.Add(new TiaGroup(plc, subPath));
            Add(plc, sub, subPath);
        }
    }
}

// Lo que pasa al subir: UDT subidos, dónde queda el FB, resultado de compilar y el FB tal como queda en TIA.
public sealed class TiaImportResult
{
    public List<string> Udts { get; } = [];
    public string? Group { get; set; }
    public string? Block { get; set; }
    // Success, Warning o Error; null si no se compiló.
    public string? CompileState { get; set; }
    public int Errors { get; set; }
    public int Warnings { get; set; }
    // Mensajes de error y aviso de la compilación: «Error · ruta: texto».
    public List<string> Messages { get; } = [];
    // Avisos (por ejemplo, que no se pudo volver a exportar el FB).
    public List<string> Notes { get; } = [];
    // El FB tal como queda en TIA, para guardarlo como FB actual; null si no se pudo exportar.
    public string? ExportedXml { get; set; }
}
