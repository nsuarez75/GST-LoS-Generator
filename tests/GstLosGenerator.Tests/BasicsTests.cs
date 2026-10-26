using GstLosGenerator.Core;

namespace GstLosGenerator.Tests;

public class BasicsTests
{
    [Theory]
    [InlineData("Bool", "Bool")]
    [InlineData("Byte", "Byte")]
    [InlineData("Int16", "Int")]
    [InlineData("Int32", "DInt")]
    [InlineData("Int64", "ULInt")]
    [InlineData("Float", "Real")]
    [InlineData("String", "Array[1..20] of Char")]
    [InlineData("String120", "Array[1..120] of Char")]
    public void Tipos_de_dato_de_la_LoS_a_TIA(string los, string tia) => Assert.Equal(tia, DataTypes.ToTia(los));

    [Fact]
    public void Nombres_de_device_instancia_y_regiones()
    {
        Assert.Equal("Device_61", Naming.DeviceName(6, 1));
        Assert.Equal("GSDML_Device62", Naming.DeviceUdt(6, 2));
        Assert.Equal(6, Naming.PlcNumber("PLC06"));
        Assert.Equal("01_1000CNV01_01_MOVECONV", Naming.WithoutLinePrefix("LX9_01_1000CNV01_01_MOVECONV"));
        Assert.Equal("1000CNV01_01_MOVECONV", Naming.OperationRegion("LX9_01_1000CNV01_01_MOVECONV"));
        Assert.Equal("CELL01_Estado", Naming.CellStateRegion("LX9_01_CELL01"));
    }

    [Fact]
    public void La_mascara_lleva_un_bit_por_referencia()
    {
        var location = new OperationLocation();
        Assert.Equal(0, location.Mask);
        location.References.AddRange(Enumerable.Range(1, 8).Select(r => new ReferenceSignals { Reference = r }));
        Assert.Equal(255, location.Mask);
        location.References.RemoveAll(r => r.Reference != 3);
        Assert.Equal(4, location.Mask);
    }

    [Fact]
    public void Arbol_de_assets_celda_operacion_y_subassets()
    {
        var (project, _) = FakeLos.ImportedProject();
        var tree = new AssetTree(project.Plcs[0].Assets);

        Assert.Equal(["LX9_01_CELL01", "LX9_76_PLC01"], tree.Roots.Select(a => a.Element));
        var cell = tree.Roots[0];
        var operation = Assert.Single(tree.Children(cell));
        Assert.Equal("LX9_01_1000FIX01_01_MOVEHR", operation.Element);
        Assert.Equal(["LX9_01_1000FIX01_01_MOVEHR_01_R", "LX9_01_1000FIX01_01_MOVEHR_02_FIX"], tree.Children(operation).Select(a => a.Element));
    }

    [Fact]
    public void El_UDT_del_device_sigue_el_formato_de_fuente_externa()
    {
        var (project, _) = FakeLos.ImportedProject();
        var plc = project.Plcs[0];

        var text = UdtWriter.Write(plc, plc.Devices[1]);

        Assert.Equal(
            "TYPE \"GSDML_Device12\"\r\nVERSION : 0.1\r\n   STRUCT\r\n"
            + "      \"String_012_o(000)\" : Array[1..20] of Char;\r\n"
            + "      \"String40_012_o(020)\" : Array[1..40] of Char;\r\n"
            + "   END_STRUCT;\r\n\r\nEND_TYPE\r\n\r\n",
            text);
        Assert.Equal("GSDML_Device12.udt", UdtWriter.FileName(plc, plc.Devices[1]));
    }

    [Fact]
    public void Proyecto_y_catalogo_se_guardan_y_se_vuelven_a_abrir_igual()
    {
        var (project, catalog) = FakeLos.ImportedProject();
        project.Plcs[0].Operations.Add(new Operation
        {
            Id = "110",
            Locations = [new OperationLocation { Location = "1000+FIX01", References = [new ReferenceSignals { Reference = 1, PartPresent = ["Sensor"] }] }],
        });
        var folder = Path.Combine(Path.GetTempPath(), "gst-tests-" + Guid.NewGuid().ToString("N"));
        try
        {
            var projectPath = Path.Combine(folder, "prueba.losproj");
            var catalogPath = Path.Combine(folder, "tipos.loscat");
            Storage.SaveProject(project, projectPath);
            Storage.SaveCatalog(catalog, catalogPath);

            Assert.Equal(Storage.ToJson(project), Storage.ToJson(Storage.LoadProject(projectPath)));
            Assert.Equal(Storage.ToJson(catalog), Storage.ToJson(Storage.LoadCatalog(catalogPath)));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
