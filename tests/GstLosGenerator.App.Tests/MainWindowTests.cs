using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using GstLosGenerator.App.ViewModels;
using GstLosGenerator.App.Views;
using GstLosGenerator.Core;
using GstLosGenerator.Core.Fb;
using GstLosGenerator.Tests;
using Location = GstLosGenerator.Core.Location;

namespace GstLosGenerator.App.Tests;

public sealed class MainWindowTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("gst-app-").FullName;

    public void Dispose()
    {
        L.Language = L.Spanish;
        App.ApplyTheme(AppSettings.ThemeSystem);
        Directory.Delete(_dir, recursive: true);
    }

    // Ventana con un proyecto ya importado, una location y una operación.
    private async Task<(MainWindow Window, MainViewModel Main, FakeDialogs Dialogs)> Open(double width = 1400)
    {
        var session = new Session(Path.Combine(_dir, "catalogo.loscat"));
        var dialogs = new FakeDialogs();
        var main = new MainViewModel(session, dialogs, new AppSettings(), _ => { });
        var los = Path.Combine(_dir, "LX9.xlsx");
        using (var file = File.Create(los))
        {
            FakeLos.Build().CopyTo(file);
        }
        dialogs.Files.Enqueue(los);
        await main.ImportLosCommand.ExecuteAsync(null);

        var plc = session.Project.Plcs[0];
        plc.Locations.Add(new Location { Name = "1000+FIX01", Number = 3 });
        plc.Operations.Add(new Operation
        {
            Id = "110",
            Locations = [new OperationLocation { Location = "1000+FIX01", References = [new ReferenceSignals { Reference = 1, PartPresent = ["++1000+FIX01-PP01"] }] }],
        });
        plc.FindAsset("LX9_01_1000FIX01_01_MOVEHR")!.Operation = "110";
        foreach (var type in session.Catalog.Types.Where(t => t.Name != "CE_PLC"))
        {
            type.Fb = "LoS_" + type.Name;
            type.Udt = "LoS_" + type.Name + "_UDT";
            type.UdtPin = type.Name;
        }
        main.Rebuild();

        var window = new MainWindow { Width = width, Height = 860 };
        window.Attach(main);
        main.Dialogs = dialogs;
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, main, dialogs);
    }

    // GST_SCREENSHOTS: carpeta en la que dejar capturas de cada pantalla.
    private static void Shot(Window window, string name)
    {
        Dispatcher.UIThread.RunJobs();
        var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        var dir = Environment.GetEnvironmentVariable("GST_SCREENSHOTS");
        if (string.IsNullOrEmpty(dir))
        {
            return;
        }
        Directory.CreateDirectory(dir);
        // Solo para revisar las pantallas a ojo; el formato PNG por defecto basta.
#pragma warning disable CS0618
        frame.Save(Path.Combine(dir, name + ".png"));
#pragma warning restore CS0618
    }

    [AvaloniaFact]
    public async Task Importar_la_LoS_crea_el_arbol_del_proyecto()
    {
        var (window, main, dialogs) = await Open();

        Assert.Single(dialogs.ChangeLists);
        var root = Assert.Single(main.Tree);
        Assert.Equal(["PLC01", "PLC02"], root.Children.Select(c => c.Title));
        Assert.Equal("LX9", main.Session.Project.Name);
        Assert.Contains("LX9", window.Title);
        Shot(window, "01-proyecto");
    }

    [AvaloniaTheory]
    [InlineData(AppSettings.ThemeLight, L.Spanish, 1400)]
    [InlineData(AppSettings.ThemeDark, L.English, 1400)]
    [InlineData(AppSettings.ThemeLight, L.Spanish, 900)]
    public async Task Todas_las_pantallas_se_dibujan(string theme, string language, double width)
    {
        L.Language = language;
        App.ApplyTheme(theme);
        var (window, main, _) = await Open(width);
        var suffix = $"{theme}-{language}-{width}";

        foreach (var kind in new[] { NavKind.Plc, NavKind.Devices, NavKind.Assets, NavKind.Operations, NavKind.Locations })
        {
            main.SelectedNode = main.FindNode(kind, "PLC01");
            Dispatcher.UIThread.RunJobs();
            Assert.NotNull(main.Editor);
            Shot(window, $"{(int)kind:00}-{kind}-{suffix}");
        }

        var catalogVm = new CatalogViewModel(main.Session, main.Dialogs);
        var catalog = new CatalogWindow { DataContext = catalogVm, Width = Math.Min(width, 1100), Height = 720 };
        catalog.Show();
        Shot(catalog, $"10-catalogo-{suffix}");
        catalogVm.Selected = catalogVm.Types.First(t => t.Name == "CE_PLC");
        catalogVm.Selected.NoFb = true;
        catalogVm.Selected.Variables[0].Source = "#LifeBit";
        Shot(catalog, $"10b-catalogo-sin-fb-{suffix}");
        catalog.Close();

        var generate = new GenerateWindow { DataContext = new GenerateViewModel(main, main.Session.Project.Plcs[0]), Width = Math.Min(width, 1040), Height = 760 };
        generate.Show();
        Shot(generate, $"11-generar-{suffix}");
        generate.Close();
    }

    [AvaloniaFact]
    public async Task Cambiar_un_tipo_en_el_arbol_se_puede_deshacer()
    {
        var (_, main, _) = await Open();
        main.SelectedNode = main.FindNode(NavKind.Assets, "PLC01");
        var assets = Assert.IsType<AssetsViewModel>(main.Editor);
        var robot = assets.Roots[0].Children.OfType<AssetNodeViewModel>().First()
            .Children.OfType<AssetNodeViewModel>().First(n => n.Element.EndsWith("_01_R", StringComparison.Ordinal));

        robot.Type = "AS_STATIC_FIXTURE";
        Assert.Equal("AS_STATIC_FIXTURE", main.Session.Project.Plcs[0].FindAsset(robot.Element)!.Type);
        Assert.True(main.Session.Dirty);

        main.UndoCommand.Execute(null);
        Assert.Equal("AS_ROBOT", main.Session.Project.Plcs[0].FindAsset(robot.Element)!.Type);
        main.RedoCommand.Execute(null);
        Assert.Equal("AS_STATIC_FIXTURE", main.Session.Project.Plcs[0].FindAsset(robot.Element)!.Type);
    }

    [AvaloniaFact]
    public async Task Al_volver_a_assets_se_conserva_lo_plegado_y_lo_seleccionado()
    {
        var (_, main, _) = await Open();
        main.SelectedNode = main.FindNode(NavKind.Assets, "PLC01");
        Dispatcher.UIThread.RunJobs();
        var assets = Assert.IsType<AssetsViewModel>(main.Editor);
        var cell = assets.Roots[0];
        var operation = cell.Children.OfType<AssetNodeViewModel>().First();
        var robot = operation.Children.OfType<AssetNodeViewModel>().First(n => n.Element.EndsWith("_01_R", StringComparison.Ordinal));
        operation.IsExpanded = true;
        robot.IsExpanded = true;
        assets.Selected = robot;
        Dispatcher.UIThread.RunJobs();

        main.SelectedNode = main.FindNode(NavKind.Operations, "PLC01");
        Dispatcher.UIThread.RunJobs();
        main.SelectedNode = main.FindNode(NavKind.Assets, "PLC01");
        Dispatcher.UIThread.RunJobs();

        var back = Assert.IsType<AssetsViewModel>(main.Editor);
        Assert.NotSame(assets, back);
        var backOperation = back.Roots[0].Children.OfType<AssetNodeViewModel>().First();
        Assert.True(backOperation.IsExpanded);
        var backRobot = Assert.IsType<AssetNodeViewModel>(back.Selected);
        Assert.Equal(robot.Element, backRobot.Element);
        Assert.True(backRobot.IsExpanded);

        // Plegar la celda también se recuerda, y lo que abre el buscador no.
        back.Roots[0].IsExpanded = false;
        back.Search = "Int16_011_o(002)";
        back.Search = "";
        main.SelectedNode = main.FindNode(NavKind.Locations, "PLC01");
        main.SelectedNode = main.FindNode(NavKind.Assets, "PLC01");
        Assert.False(Assert.IsType<AssetsViewModel>(main.Editor).Roots[0].IsExpanded);
    }

    [AvaloniaFact]
    public async Task El_buscador_filtra_por_direccion_y_abre_el_camino()
    {
        var (_, main, _) = await Open();
        main.SelectedNode = main.FindNode(NavKind.Assets, "PLC01");
        var assets = Assert.IsType<AssetsViewModel>(main.Editor);

        assets.Search = "Int16_011_o(002)";

        var cell = Assert.Single(assets.Roots);
        Assert.True(cell.IsExpanded);
        var operation = Assert.Single(cell.Children.OfType<AssetNodeViewModel>());
        Assert.Equal("LX9_01_1000FIX01_01_MOVEHR_01_R", Assert.Single(operation.Children.OfType<AssetNodeViewModel>()).Element);
    }

    [AvaloniaFact]
    public async Task Asignar_una_operacion_desde_la_ficha_se_ve_en_el_arbol()
    {
        var (_, main, _) = await Open();
        main.SelectedNode = main.FindNode(NavKind.Operations, "PLC01");
        var operations = Assert.IsType<OperationsViewModel>(main.Editor);
        var check = operations.Selected!.Assets.First(a => a.Element == "LX9_01_1000FIX01_01_MOVEHR_02_FIX");

        check.IsChecked = true;
        operations.Selected.Locations[0].ReferenceToggles[1].IsChecked = true;

        var plc = main.Session.Project.Plcs[0];
        Assert.Equal("110", plc.FindAsset("LX9_01_1000FIX01_01_MOVEHR_02_FIX")!.Operation);
        Assert.Equal(3, plc.Operations[0].Locations[0].Mask);
        Assert.Equal(["++1000+FIX01-PP01"], plc.Operations[0].Locations[0].References[1].PartPresent);
        main.SelectedNode = main.FindNode(NavKind.Assets, "PLC01");
        var assets = Assert.IsType<AssetsViewModel>(main.Editor);
        assets.Select("LX9_01_1000FIX01_01_MOVEHR_02_FIX");
        Assert.Equal("110", Assert.IsType<AssetNodeViewModel>(assets.Selected).Operation);
    }

    [AvaloniaFact]
    public async Task Arrastrar_una_operacion_cambia_el_orden_y_se_puede_volver_al_de_numero()
    {
        var (_, main, _) = await Open();
        var plc = main.Session.Project.Plcs[0];
        plc.Operations.Add(new Operation { Id = "90" });
        main.SelectedNode = main.FindNode(NavKind.Operations, "PLC01");
        var operations = Assert.IsType<OperationsViewModel>(main.Editor);
        Assert.Equal(["90", "110"], operations.Operations.Select(o => o.Id));

        operations.Move(operations.Operations[1], 0);

        Assert.Equal(["110", "90"], operations.Operations.Select(o => o.Id));
        Assert.Equal(["110", "90"], plc.OrderedOperations().Select(o => o.Id));
        Assert.True(operations.HasCustomOrder);

        operations.SortByNumberCommand.Execute(null);
        Assert.Equal(["90", "110"], operations.Operations.Select(o => o.Id));
        Assert.False(main.Session.Project.Plcs[0].CustomOperationOrder);

        main.UndoCommand.Execute(null);
        Assert.Equal(["110", "90"], main.Session.Project.Plcs[0].OrderedOperations().Select(o => o.Id));
    }

    [AvaloniaFact]
    public async Task Arrastrar_con_el_raton_una_operacion_la_lleva_a_otra_posicion()
    {
        var (window, main, _) = await Open();
        main.Session.Project.Plcs[0].Operations.Add(new Operation { Id = "90" });
        main.SelectedNode = main.FindNode(NavKind.Operations, "PLC01");
        Dispatcher.UIThread.RunJobs();
        var operations = Assert.IsType<OperationsViewModel>(main.Editor);
        var items = window.GetVisualDescendants().OfType<ListBoxItem>().Where(i => i.DataContext is OperationViewModel).ToList();
        Point Top(ListBoxItem item) => item.TranslatePoint(new Point(40, 6), window)!.Value;

        // La 110 (segunda) se suelta en la mitad de arriba de la 90 (primera).
        var start = Top(items[1]);
        window.MouseDown(start, MouseButton.Left);
        window.MouseMove(start + new Point(0, -10), RawInputModifiers.LeftMouseButton);
        Dispatcher.UIThread.RunJobs();
        window.MouseMove(Top(items[0]), RawInputModifiers.LeftMouseButton);
        Dispatcher.UIThread.RunJobs();
        window.MouseUp(Top(items[0]), MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["110", "90"], operations.Operations.Select(o => o.Id));
    }

    private static List<string> VariableNames(MainViewModel main, string element) =>
        main.Session.Project.Plcs[0].FindAsset(element)!.Variables.Select(v => v.Name).ToList();

    [AvaloniaFact]
    public async Task Con_mayus_clic_se_seleccionan_varias_variables_y_se_arrastran_a_otro_asset()
    {
        var (window, main, _) = await Open();
        main.SelectedNode = main.FindNode(NavKind.Assets, "PLC01");
        Dispatcher.UIThread.RunJobs();
        Point Grip(string name) => window.GetVisualDescendants().OfType<Border>()
            .First(b => b.Classes.Contains("grip") && b.DataContext is VariableRowViewModel r && r.Name == name)
            .TranslatePoint(new Point(4, 8), window)!.Value;

        window.MouseDown(Grip("DoorOpen01"), MouseButton.Left);
        window.MouseUp(Grip("DoorOpen01"), MouseButton.Left);
        window.MouseDown(Grip("CellWorkingEvent"), MouseButton.Left, RawInputModifiers.Shift);
        window.MouseUp(Grip("CellWorkingEvent"), MouseButton.Left, RawInputModifiers.Shift);
        Dispatcher.UIThread.RunJobs();
        var tree = window.GetVisualDescendants().OfType<TreeView>().Single(t => t.Name == "Tree");
        Assert.Equal(2, tree.SelectedItems!.OfType<VariableRowViewModel>().Count());

        var target = window.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == "LX9_01_1000FIX01_01_MOVEHR")
            .TranslatePoint(new Point(20, 6), window)!.Value;
        var start = Grip("CellWorkingEvent");
        window.MouseDown(start, MouseButton.Left);
        window.MouseMove(start + new Point(0, 12), RawInputModifiers.LeftMouseButton);
        Dispatcher.UIThread.RunJobs();
        window.MouseMove(target, RawInputModifiers.LeftMouseButton);
        Dispatcher.UIThread.RunJobs();
        window.MouseUp(target, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["ProductionStatus"], VariableNames(main, "LX9_01_CELL01"));
        Assert.Contains("DoorOpen01", VariableNames(main, "LX9_01_1000FIX01_01_MOVEHR"));
        Assert.Contains("CellWorkingEvent", VariableNames(main, "LX9_01_1000FIX01_01_MOVEHR"));
        Shot(window, "03b-variables-movidas");
    }

    [AvaloniaFact]
    public async Task Borrar_varias_variables_de_golpe_se_deshace_de_una_vez()
    {
        var (_, main, _) = await Open();
        main.SelectedNode = main.FindNode(NavKind.Assets, "PLC01");
        var assets = Assert.IsType<AssetsViewModel>(main.Editor);
        var cell = assets.Roots[0];

        assets.RemoveVariables(cell.Children.OfType<VariableRowViewModel>().Skip(1).ToList());
        Assert.Equal(["ProductionStatus"], VariableNames(main, "LX9_01_CELL01"));
        Assert.Single(cell.Children.OfType<VariableRowViewModel>());

        main.UndoCommand.Execute(null);
        Assert.Equal(["ProductionStatus", "DoorOpen01", "CellWorkingEvent"], VariableNames(main, "LX9_01_CELL01"));
    }

    [AvaloniaFact]
    public async Task Mover_una_variable_que_el_destino_ya_tiene_avisa_y_no_la_mueve()
    {
        var (_, main, dialogs) = await Open();
        main.SelectedNode = main.FindNode(NavKind.Assets, "PLC01");
        var assets = Assert.IsType<AssetsViewModel>(main.Editor);
        var cell = assets.Roots[0];
        var operation = cell.Children.OfType<AssetNodeViewModel>().First();
        main.Session.Project.Plcs[0].FindAsset(operation.Element)!.Variables.Add(new AssetVariable { Name = "DoorOpen01" });
        var rows = cell.Children.OfType<VariableRowViewModel>().Where(r => r.Name is "DoorOpen01" or "CellWorkingEvent").ToList();

        await assets.MoveVariables(rows, operation);

        Assert.Equal(["ProductionStatus", "DoorOpen01"], VariableNames(main, "LX9_01_CELL01"));
        Assert.Contains(dialogs.Messages, m => m.Contains("DoorOpen01"));
        Assert.Contains(dialogs.Messages, m => m.Contains("CellWorkingEvent") && m.Contains("UDT"));
    }

    [AvaloniaFact]
    public async Task Al_reimportar_la_LoS_se_pregunta_por_cada_variable_movida()
    {
        var (_, main, dialogs) = await Open();
        main.SelectedNode = main.FindNode(NavKind.Assets, "PLC01");
        var assets = Assert.IsType<AssetsViewModel>(main.Editor);
        var cell = assets.Roots[0];
        var operation = cell.Children.OfType<AssetNodeViewModel>().First();
        await assets.MoveVariables(cell.Children.OfType<VariableRowViewModel>().Where(r => r.Name is "DoorOpen01" or "CellWorkingEvent").ToList(), operation);

        var los = Path.Combine(_dir, "LX9.xlsx");
        dialogs.Files.Enqueue(los);
        dialogs.Choices.Enqueue(true);
        dialogs.Choices.Enqueue(false);
        await main.ImportLosCommand.ExecuteAsync(null);

        Assert.Equal(["ProductionStatus", "CellWorkingEvent"], VariableNames(main, "LX9_01_CELL01"));
        Assert.Contains("DoorOpen01", VariableNames(main, "LX9_01_1000FIX01_01_MOVEHR"));
        Assert.Equal("DoorOpen01", Assert.Single(main.Session.Project.Plcs[0].VariableMoves).Name);
    }

    [AvaloniaFact]
    public async Task En_el_catalogo_se_aprende_un_tipo_de_un_FB_eligiendo_la_llamada()
    {
        var (_, main, dialogs) = await Open();
        var generate = new GenerateViewModel(main, main.Session.Project.Plcs[0]);
        dialogs.Folder = Path.Combine(_dir, "salida");
        await generate.GenerateCommand.ExecuteAsync(null);
        var catalog = new CatalogViewModel(main.Session, dialogs);
        dialogs.Prompts.Enqueue(["ROBOT_NUEVO"]);
        await catalog.AddCommand.ExecuteAsync(null);
        var options = TypeLearner.Sources(File.ReadAllText(Path.Combine(dialogs.Folder, "User_LoS_Device_In_FB.xml")));
        dialogs.PickAnswer = options.FindIndex(o => o.Fb == "LoS_AS_ROBOT");
        dialogs.Files.Enqueue(Path.Combine(dialogs.Folder, "User_LoS_Device_In_FB.xml"));

        await catalog.LearnFromFbCommand.ExecuteAsync(null);

        Assert.Equal(options.Select(o => o.Label), Assert.Single(dialogs.PickLists));
        var type = main.Session.Catalog.Find("ROBOT_NUEVO")!;
        Assert.Equal(("LoS_AS_ROBOT", "LoS_AS_ROBOT_UDT", "AS_ROBOT"), (type.Fb, type.Udt, type.UdtPin));
        Assert.Equal("ROBOT_NUEVO", catalog.Selected!.Name);
        main.UndoCommand.Execute(null);
        Assert.Equal("", main.Session.Catalog.Find("ROBOT_NUEVO")!.Fb);
    }

    [AvaloniaFact]
    public async Task El_comentario_de_una_operacion_cambia_su_titulo()
    {
        var (_, main, _) = await Open();
        main.SelectedNode = main.FindNode(NavKind.Operations, "PLC01");
        var operation = Assert.IsType<OperationsViewModel>(main.Editor).Selected!;

        operation.Comment = "  Carga de piezas ";

        Assert.Equal("Operation 110: Carga de piezas", operation.Title);
        Assert.Equal("Carga de piezas", main.Session.Project.Plcs[0].Operations[0].Comment);
    }

    [AvaloniaFact]
    public async Task Generar_escribe_el_FB_y_los_UDT_en_la_carpeta_de_salida()
    {
        var (_, main, dialogs) = await Open();
        var output = Path.Combine(_dir, "salida");
        dialogs.Folder = output;
        var generate = new GenerateViewModel(main, main.Session.Project.Plcs[0]);

        await generate.GenerateCommand.ExecuteAsync(null);

        Assert.True(File.Exists(Path.Combine(output, "User_LoS_Device_In_FB.xml")));
        Assert.True(File.Exists(Path.Combine(output, "GSDML_Device11.udt")));
        Assert.True(File.Exists(Path.Combine(output, "GSDML_Device12.udt")));
        Assert.Equal(output, main.Session.Project.OutputFolder);
        Assert.Contains(dialogs.Messages, m => m.Contains("LX9_76_PLC01"));
    }

    [AvaloniaFact]
    public async Task Guardar_y_abrir_el_proyecto()
    {
        var (_, main, dialogs) = await Open();
        var path = Path.Combine(_dir, "LX9.losproj");
        dialogs.Files.Enqueue(path);

        await main.SaveAsCommand.ExecuteAsync(null);
        Assert.True(File.Exists(path));
        Assert.True(File.Exists(main.Session.CatalogPath));
        Assert.False(main.Session.Dirty);

        main.Session.New();
        dialogs.Files.Enqueue(path);
        await main.OpenCommand.ExecuteAsync(null);
        Assert.Equal("LX9", main.Session.Project.Name);
        Assert.Equal(2, main.Session.Project.Plcs.Count);
    }

    [AvaloniaFact]
    public async Task Los_avisos_llevan_a_lo_que_los_provoca()
    {
        var (_, main, _) = await Open();

        var issue = main.Issues.First(i => i.Issue.Target == IssueTarget.Asset && i.Issue.Subject == "LX9_76_PLC01");
        main.SelectedIssue = issue;

        Assert.Equal(NavKind.Assets, main.SelectedNode!.Kind);
        var assets = Assert.IsType<AssetsViewModel>(main.Editor);
        Assert.Equal("LX9_76_PLC01", Assert.IsType<AssetNodeViewModel>(assets.Selected).Element);
    }

    [AvaloniaFact]
    public async Task Acerca_de_muestra_el_autor()
    {
        var (_, main, dialogs) = await Open();

        await main.AboutCommand.ExecuteAsync(null);

        Assert.Contains("Acerca de: Creado por Ignacio Suarez. https://github.com/nsuarez75/GST-LoS-Generator", dialogs.Messages);
    }

    [AvaloniaFact]
    public async Task Avisa_si_hay_una_release_mas_nueva()
    {
        var (window, main, _) = await Open();

        await main.CheckForUpdates(_ => Task.FromResult<ReleaseInfo?>(new ReleaseInfo("v99.0.0", new Version(99, 0, 0), "https://github.com/x/y/releases/tag/v99.0.0")));

        Assert.True(main.HasUpdate);
        Assert.Contains("v99.0.0", main.UpdateNotice);
        Shot(window, "12-version-nueva");
        main.DismissUpdateCommand.Execute(null);
        Assert.False(main.HasUpdate);

        await main.CheckForUpdates(_ => Task.FromResult<ReleaseInfo?>(null));
        Assert.False(main.HasUpdate);
    }
}
