using GstLosGenerator.Core;

namespace GstLosGenerator.Tests;

public class LosTests
{
    [Fact]
    public void Lee_los_plcs_assets_y_variables_en_el_orden_de_la_LoS()
    {
        var data = FakeLos.Read();

        Assert.Equal(["PLC01", "PLC02"], data.Plcs.Select(p => p.Name));
        var plc = data.Plcs[0];
        Assert.Equal(1, plc.Number);
        Assert.Equal(
            ["LX9_01_CELL01", "LX9_01_1000FIX01_01_MOVEHR", "LX9_01_1000FIX01_01_MOVEHR_01_R", "LX9_01_1000FIX01_01_MOVEHR_02_FIX", "LX9_76_PLC01"],
            plc.Assets.Select(a => a.Element));
        var cell = plc.Assets[0];
        Assert.Equal(AssetKind.Cell, cell.Kind);
        Assert.Equal("CE_CELL", cell.Type);
        Assert.Equal(3, cell.Variables.Count);
        Assert.Equal("String_012_o(020)", cell.Variables[2].Address);
        Assert.Equal(AssetKind.Operation, plc.Assets[1].Kind);
        Assert.Equal(AssetKind.Asset, plc.Assets[2].Kind);
        Assert.Equal(AssetKind.Other, plc.Assets[4].Kind);
        Assert.False(plc.Assets[4].Variables[1].IsOutput);
    }

    [Fact]
    public void Los_devices_salen_de_la_hoja_OUT_con_el_tipo_completo_en_los_String()
    {
        var plc = FakeLos.Read().Plcs[0];

        Assert.Equal([1, 2], plc.Devices.Select(d => d.Number));
        Assert.Equal(13, plc.Devices[0].Addresses.Count);
        Assert.Equal("Int64", plc.Devices[0].Addresses[^1].DataType);
        var strings = plc.Devices[1].Addresses;
        Assert.Equal("String_012_o(000)", strings[0].Name);
        Assert.Equal("String40_012_o(020)", strings[1].Name);
        Assert.Equal("String_012_o(020)", strings[1].SheetName);
    }

    [Fact]
    public void Sin_hoja_LoS_avisa_con_un_error_claro()
    {
        using var empty = new ClosedXML.Excel.XLWorkbook();
        empty.AddWorksheet("Otra");
        var stream = new MemoryStream();
        empty.SaveAs(stream);
        stream.Position = 0;

        var error = Assert.Throws<LosFormatException>(() => LosReader.Read(stream));
        Assert.Contains("LoS", error.Message);
    }

    [Fact]
    public void Importar_en_un_proyecto_vacio_crea_plcs_y_tipos_pendientes_de_equivalencia()
    {
        var project = new Project();
        var catalog = new Catalog();

        var plan = LosImportPlan.Create(project, catalog, FakeLos.Read());
        Assert.Contains(plan.Changes, c => c.Kind == ChangeKind.PlcAdded && c.Subject == "PLC01");
        Assert.Contains(plan.Changes, c => c.Kind == ChangeKind.TypeAdded && c.Subject == "AS_ROBOT");
        plan.Apply();

        Assert.Equal(2, project.Plcs.Count);
        Assert.Equal(5, project.Plcs[0].Assets.Count);
        var robot = catalog.Find("AS_ROBOT")!;
        Assert.False(robot.HasEquivalence);
        Assert.Equal(["Job", "Torque"], robot.Variables.Select(v => v.Name));
    }

    [Fact]
    public void Una_LoS_nueva_conserva_lo_hecho_a_mano_y_marca_lo_que_desaparece()
    {
        var (project, catalog) = FakeLos.ImportedProject();
        var plc = project.Plcs[0];
        plc.Operations.Add(new Operation { Id = "110" });
        plc.FindAsset("LX9_01_1000FIX01_01_MOVEHR")!.Operation = "110";
        plc.FindAsset("LX9_01_1000FIX01_01_MOVEHR_01_R")!.Type = "AS_ROBOT_B";
        plc.Assets.Add(new Asset { Element = "LX9_01_EXTRA", Type = "AS_ROBOT", Manual = true });
        plc.FindAsset("LX9_01_1000FIX01_01_MOVEHR_01_R")!.Variables.First(v => v.Name == "Job").Disabled = true;

        var signals = FakeLos.DefaultSignals();
        signals.RemoveAll(s => s.Element == "LX9_01_1000FIX01_01_MOVEHR_02_FIX");
        signals.Add(new("PLC01", "A", "LX9_01_1000FIX01_01_MOVEHR_03_R", "Job", "AS_ROBOT", "Int16", "OUT", "Int16_011_o(003)"));
        signals.Add(new("PLC01", "", "LX9_01_1000FIX01_01_MOVEHR_01_R", "Speed", "AS_ROBOT", "Int16", "OUT", "Int16_011_o(003)"));
        var plan = LosImportPlan.Create(project, catalog, FakeLos.Read(signals));

        Assert.Contains(plan.Changes, c => c.Kind == ChangeKind.AssetAdded && c.Subject == "LX9_01_1000FIX01_01_MOVEHR_03_R");
        Assert.Contains(plan.Changes, c => c.Kind == ChangeKind.AssetMissing && c.Subject == "LX9_01_1000FIX01_01_MOVEHR_02_FIX");
        Assert.Contains(plan.Changes, c => c.Kind == ChangeKind.VariablesChanged && c.Subject == "LX9_01_1000FIX01_01_MOVEHR_01_R");
        Assert.Contains(plan.Changes, c => c.Kind == ChangeKind.TypeVariableAdded && c.Subject == "AS_ROBOT" && c.Detail == "Speed");
        Assert.DoesNotContain(plan.Changes, c => c.Subject == "LX9_01_EXTRA");
        plan.Apply();

        Assert.Equal("110", plc.FindAsset("LX9_01_1000FIX01_01_MOVEHR")!.Operation);
        Assert.Equal("AS_ROBOT_B", plc.FindAsset("LX9_01_1000FIX01_01_MOVEHR_01_R")!.Type);
        Assert.True(plc.FindAsset("LX9_01_1000FIX01_01_MOVEHR_02_FIX")!.Missing);
        Assert.False(plc.FindAsset("LX9_01_EXTRA")!.Missing);
        Assert.Single(plc.Operations);
        Assert.Contains(catalog.Find("AS_ROBOT")!.Variables, v => v.Name == "Speed");
        var robot = plc.FindAsset("LX9_01_1000FIX01_01_MOVEHR_01_R")!.Variables;
        Assert.True(robot.First(v => v.Name == "Job").Disabled);
        Assert.False(robot.First(v => v.Name == "Speed").Disabled);
    }

    [Fact]
    public void Un_asset_manual_que_trae_la_LoS_pasa_a_ser_de_la_LoS()
    {
        var project = new Project();
        project.Plcs.Add(new Plc { Name = "PLC02", Number = 2 });
        project.Plcs[0].Assets.Add(new Asset { Element = "LX9_02_CELL02", Type = "CE_CELL", Manual = true });

        var plan = LosImportPlan.Create(project, new Catalog(), FakeLos.Read());
        Assert.Contains(plan.Changes, c => c.Kind == ChangeKind.AssetAdopted);
        plan.Apply();

        Assert.False(project.FindPlc("PLC02")!.FindAsset("LX9_02_CELL02")!.Manual);
    }
}
