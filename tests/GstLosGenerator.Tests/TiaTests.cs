using GstLosGenerator.Core;
using GstLosGenerator.Core.Fb;
using GstLosGenerator.Core.Tia;

namespace GstLosGenerator.Tests;

public class TiaTests
{
    [Fact]
    public void El_arbol_se_monta_con_lo_que_dice_el_puente()
    {
        var tree = new TiaTree();
        foreach (var line in new[]
                 {
                     "PLC PLC_01",
                     "BLOCK PLC_01||Main|OB",
                     "GROUP PLC_01|LoS",
                     "BLOCK PLC_01|LoS|User_LoS_Device_In_FB|FB",
                     "GROUP PLC_01|LoS/Tipos",
                     "BLOCK PLC_01|LoS/Tipos|LoS_OP_MOVE_CNV|FB",
                     "PROGRESS Abriendo…",
                 })
        {
            var (key, text) = TiaProtocol.Split(line);
            TiaProtocol.ApplyTree(tree, key, text);
        }

        Assert.Equal(["PLC_01"], tree.Plcs);
        Assert.Equal(["LoS", "LoS/Tipos"], tree.Groups.Select(g => g.Path));
        Assert.Equal(new TiaBlock("PLC_01", "LoS", "User_LoS_Device_In_FB", "FB"), tree.FindBlock("PLC_01", "User_LoS_Device_In_FB"));
        Assert.Null(tree.FindBlock("PLC_02", "User_LoS_Device_In_FB"));
    }

    [Fact]
    public void El_resultado_de_subir_recoge_UDT_compilacion_y_mensajes()
    {
        var result = new TiaImportResult();
        foreach (var line in new[]
                 {
                     "UDT GSDML_Device11",
                     "PLACED LoS|User_LoS_Device_In_FB",
                     "COMPILE Warning|0|2",
                     "MSG Warning|User_LoS_Device_In_FB|Variable sin usar | aviso",
                     "EXPORTED C:\\tmp\\actual.xml",
                 })
        {
            var (key, text) = TiaProtocol.Split(line);
            TiaProtocol.ApplyImport(result, key, text);
        }

        Assert.Equal(["GSDML_Device11"], result.Udts);
        Assert.Equal(("LoS", "User_LoS_Device_In_FB"), (result.Group, result.Block));
        Assert.Equal(("Warning", 0, 2), (result.CompileState, result.Errors, result.Warnings));
        Assert.Equal("Warning · User_LoS_Device_In_FB: Variable sin usar | aviso", Assert.Single(result.Messages));
        Assert.Equal("C:\\tmp\\actual.xml", result.ExportedFile);
    }

    [Fact]
    public void Las_ordenes_van_separadas_por_tabuladores_y_no_admiten_saltos()
    {
        Assert.Equal("export\tC:\\a b\\L.ap19\tPLC_01\t\tFB", TiaProtocol.Command("export", "C:\\a b\\L.ap19", "PLC_01", "", "FB"));
        Assert.Throws<ArgumentException>(() => TiaProtocol.Command("tree", "a\nb"));
        Assert.Equal(("DONE", ""), TiaProtocol.Split("DONE"));
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
