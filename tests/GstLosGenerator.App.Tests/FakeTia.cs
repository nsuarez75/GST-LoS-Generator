using GstLosGenerator.App.ViewModels;
using GstLosGenerator.Core;
using GstLosGenerator.Core.Tia;

namespace GstLosGenerator.App.Tests;

// TIA de prueba: un proyecto abierto con un PLC y los bloques que prepare el test; responde al momento y apunta lo
// que se le pide.
internal sealed class FakeTia : ITia
{
    public const string ProjectPath = @"C:\Proyectos\Linea\Linea.ap19";

    public List<string> OpenProjects { get; } = [ProjectPath];
    public TiaTree Tree { get; } = new();
    // XML de cada bloque, por «PLC/carpeta/bloque».
    public Dictionary<string, string> Blocks { get; } = [];
    public List<(TiaLink Destination, string FbXml, IReadOnlyList<(string File, string Text)> Udts)> Imports { get; } = [];
    public int Sessions { get; private set; }
    public int Closed { get; private set; }

    public bool Available => true;

    public FakeTia(string plc = "PLC_01")
    {
        Tree.Plcs.Add(plc);
        Tree.Groups.Add(new TiaGroup(plc, "LoS"));
    }

    public static string Key(string plc, string group, string block) => $"{plc}/{group}/{block}";

    public void AddBlock(string plc, string group, string name, string xml)
    {
        Tree.Blocks.Add(new TiaBlock(plc, group, name, "FB"));
        Blocks[Key(plc, group, name)] = xml;
    }

    public ITiaSession Open()
    {
        Sessions++;
        return new Session(this);
    }

    private sealed class Session(FakeTia tia) : ITiaSession
    {
        public Task<IReadOnlyList<string>> OpenProjects() => Task.FromResult<IReadOnlyList<string>>(tia.OpenProjects);

        public Task<TiaTree> Tree(string project, IProgress<string> progress) => Task.FromResult(tia.Tree);

        public Task<string> Export(TiaLink block, IProgress<string> progress) =>
            tia.Blocks.TryGetValue(Key(block.Plc, block.Group, block.Block), out var xml)
                ? Task.FromResult(xml)
                : throw new TiaException("No existe el bloque " + block.Block);

        public Task<TiaImportResult> Import(TiaLink destination, string fbXml, IReadOnlyList<(string File, string Text)> udts, IProgress<string> progress)
        {
            tia.Imports.Add((destination, fbXml, udts));
            var inTia = fbXml;
            if (tia.Tree.FindBlock(destination.Plc, Naming.FbName) is null)
            {
                tia.Tree.Blocks.Add(new TiaBlock(destination.Plc, destination.Group, Naming.FbName, "FB"));
            }
            tia.Blocks[Key(destination.Plc, destination.Group, Naming.FbName)] = inTia;
            var result = new TiaImportResult
            {
                Group = destination.Group,
                Block = Naming.FbName,
                CompileState = "Success",
                ExportedXml = inTia,
            };
            result.Udts.AddRange(udts.Select(u => Path.GetFileNameWithoutExtension(u.File)));
            return Task.FromResult(result);
        }

        public void Dispose() => tia.Closed++;
    }
}
