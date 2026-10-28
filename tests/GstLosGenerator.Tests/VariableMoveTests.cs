using GstLosGenerator.Core;

namespace GstLosGenerator.Tests;

public class VariableMoveTests
{
    private const string Robot = "LX9_01_1000FIX01_01_MOVEHR_01_R";
    private const string Fixture = "LX9_01_1000FIX01_01_MOVEHR_02_FIX";

    // Mueve Torque del robot a la fixture.
    private static (Project Project, Catalog Catalog, Plc Plc) Moved()
    {
        var (project, catalog) = FakeLos.ImportedProject();
        var plc = project.Plcs[0];
        var robot = plc.FindAsset(Robot)!;
        VariableMover.Move(plc, catalog, robot, plc.FindAsset(Fixture)!, robot.Variables.First(v => v.Name == "Torque"));
        return (project, catalog, plc);
    }

    private static List<string> Names(Plc plc, string element) => plc.FindAsset(element)!.Variables.Select(v => v.Name).ToList();

    [Fact]
    public void Mover_una_variable_la_lleva_con_sus_datos_y_apunta_el_movimiento()
    {
        var (_, catalog, plc) = Moved();

        Assert.DoesNotContain("Torque", Names(plc, Robot));
        var torque = plc.FindAsset(Fixture)!.Variables.Single(v => v.Name == "Torque");
        Assert.Equal("Float_011_o(006)", torque.Address);
        var move = Assert.Single(plc.VariableMoves);
        Assert.Equal((Robot, Fixture, "Torque"), (move.From, move.To, move.Name));
        Assert.Contains(catalog.Find("AS_STATIC_FIXTURE")!.Variables, v => v.Name == "Torque");
    }

    [Fact]
    public void Si_el_destino_ya_tiene_la_variable_no_se_puede_mover()
    {
        var (_, _, plc) = Moved();
        var robot = plc.FindAsset(Robot)!;

        Assert.NotNull(VariableMover.CannotMove(robot, new AssetVariable { Name = "Job" }));
        Assert.Null(VariableMover.CannotMove(robot, new AssetVariable { Name = "Otra" }));
    }

    [Fact]
    public void Devolver_la_variable_a_su_origen_borra_el_movimiento()
    {
        var (_, catalog, plc) = Moved();
        var fixture = plc.FindAsset(Fixture)!;

        VariableMover.Move(plc, catalog, fixture, plc.FindAsset(Robot)!, fixture.Variables.First(v => v.Name == "Torque"));

        Assert.Empty(plc.VariableMoves);
    }

    [Fact]
    public void Mover_dos_veces_apunta_el_origen_de_la_LoS_y_el_destino_final()
    {
        var (_, catalog, plc) = Moved();
        var fixture = plc.FindAsset(Fixture)!;

        VariableMover.Move(plc, catalog, fixture, plc.FindAsset("LX9_01_CELL01")!, fixture.Variables.First(v => v.Name == "Torque"));

        var move = Assert.Single(plc.VariableMoves);
        Assert.Equal((Robot, "LX9_01_CELL01"), (move.From, move.To));
    }

    [Fact]
    public void Al_reimportar_se_pregunta_y_si_se_mantiene_la_variable_sigue_movida()
    {
        var (project, catalog, plc) = Moved();
        plc.FindAsset(Fixture)!.Variables.Single(v => v.Name == "Torque").Disabled = true;

        var plan = LosImportPlan.Create(project, catalog, FakeLos.Read());
        var question = Assert.Single(plan.MovedVariables);
        Assert.Equal("Torque", question.Move.Name);
        // Con la variable movida, la LoS no trae cambios en esos assets.
        Assert.DoesNotContain(plan.Changes, c => c.Kind == ChangeKind.VariablesChanged);
        plan.Apply();

        Assert.DoesNotContain("Torque", Names(plc, Robot));
        Assert.True(plc.FindAsset(Fixture)!.Variables.Single(v => v.Name == "Torque").Disabled);
        Assert.Single(plc.VariableMoves);
    }

    [Fact]
    public void Al_reimportar_si_no_se_mantiene_manda_la_LoS_y_se_olvida_el_movimiento()
    {
        var (project, catalog, plc) = Moved();

        var plan = LosImportPlan.Create(project, catalog, FakeLos.Read());
        plan.MovedVariables[0].Keep = false;
        plan.Refresh();
        Assert.Contains(plan.Changes, c => c.Kind == ChangeKind.VariablesChanged && c.Subject == Robot);
        plan.Apply();

        Assert.Contains("Torque", Names(plc, Robot));
        Assert.DoesNotContain("Torque", Names(plc, Fixture));
        Assert.Empty(plc.VariableMoves);
    }

    [Fact]
    public void Si_la_LoS_ya_no_trae_la_variable_en_el_origen_el_movimiento_se_olvida()
    {
        var (project, catalog, plc) = Moved();
        var signals = FakeLos.DefaultSignals().Where(s => s.Signal != "Torque").ToList();

        var plan = LosImportPlan.Create(project, catalog, FakeLos.Read(signals));
        Assert.Empty(plan.MovedVariables);
        plan.Apply();

        Assert.Empty(plc.VariableMoves);
        Assert.DoesNotContain("Torque", Names(plc, Fixture));
    }
}
