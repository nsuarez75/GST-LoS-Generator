using GstLosGenerator.Core;
using GstLosGenerator.Core.Fb;
using GstLosGenerator.Core.Tia;
using TiaConnector.Contracts;

namespace GstLosGenerator.Tests;

public class TiaTests
{
    [Fact]
    public void El_arbol_de_TIA_pasa_al_de_la_app_con_la_ruta_de_cada_carpeta()
    {
        var tipos = new BlockFolder { Name = "Tipos", Blocks = { new BlockInfo { Name = "LoS_OP_MOVE_CNV", Type = "FB" } } };
        var los = new BlockFolder
        {
            Name = "LoS",
            Folders = { tipos },
            Blocks = { new BlockInfo { Name = "User_LoS_Device_In_FB", Type = "FB" } },
        };
        var root = new BlockFolder { Name = "Bloques de programa", Folders = { los }, Blocks = { new BlockInfo { Name = "Main", Type = "OB" } } };

        var tree = TiaTree.From([new PlcTree { Name = "PLC_01", Root = root }]);

        Assert.Equal(["PLC_01"], tree.Plcs);
        Assert.Equal(["LoS", "LoS/Tipos"], tree.Groups.Select(g => g.Path));
        Assert.Equal(new TiaBlock("PLC_01", "", "Main", "OB"), tree.FindBlock("PLC_01", "Main"));
        Assert.Equal(new TiaBlock("PLC_01", "LoS", "User_LoS_Device_In_FB", "FB"), tree.FindBlock("PLC_01", "User_LoS_Device_In_FB"));
        Assert.Equal(new TiaBlock("PLC_01", "LoS/Tipos", "LoS_OP_MOVE_CNV", "FB"), tree.FindBlock("PLC_01", "LoS_OP_MOVE_CNV"));
        Assert.Null(tree.FindBlock("PLC_02", "User_LoS_Device_In_FB"));
    }

    [Fact]
    public void El_mismo_FB_exportado_otra_vez_cuenta_como_el_mismo()
    {
        const string first = """
            <?xml version="1.0" encoding="utf-8"?>
            <Document>
              <Engineering version="V19" />
              <DocumentInfo>
                <Created>2026-11-01T10:00:01.1234567Z</Created>
                <ExportSetting>WithDefaults</ExportSetting>
              </DocumentInfo>
              <SW.Blocks.FB ID="0">
                <AttributeList>
                  <ModifiedDate>2026-11-01T10:00:00Z</ModifiedDate>
                  <Name>User_LoS_Device_In_FB</Name>
                </AttributeList>
              </SW.Blocks.FB>
            </Document>
            """;
        // Otra exportación del mismo FB: otra hora en la cabecera y en la fecha de modificación.
        var again = first.Replace("2026-11-01T10:00:01.1234567Z", "2026-11-10T09:15:42.7654321Z")
            .Replace("2026-11-01T10:00:00Z", "2026-11-09T08:30:00Z").Replace("  ", "    ");
        var changed = first.Replace("User_LoS_Device_In_FB", "Otro_FB");

        Assert.True(FbCompare.Same(first, again));
        Assert.False(FbCompare.Same(first, changed));
        Assert.False(FbCompare.Same(first, "no es XML"));
    }

    [Fact]
    public void El_destino_de_TIA_se_guarda_con_el_proyecto()
    {
        var path = Path.Combine(Directory.CreateTempSubdirectory("gst-tia-").FullName, "p.losproj");
        var project = new Project { Name = "P" };
        project.Plcs.Add(new Plc { Name = "PLC01", Tia = new TiaLink { Project = @"C:\L\L.ap19", Plc = "PLC_01", Group = "LoS", Block = "User_LoS_Device_In_FB" } });

        Storage.SaveProject(project, path);
        var link = Storage.LoadProject(path).Plcs[0].Tia!;

        Assert.Equal((@"C:\L\L.ap19", "PLC_01", "LoS", "User_LoS_Device_In_FB"), (link.Project, link.Plc, link.Group, link.Block));
        Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
    }
}
