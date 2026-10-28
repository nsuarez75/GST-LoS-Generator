using System.Xml.Linq;
using GstLosGenerator.Core;
using GstLosGenerator.Core.Fb;

namespace GstLosGenerator.Tests;

public class FbTests
{
    private static (Project Project, Catalog Catalog, Plc Plc) Prepared()
    {
        var (project, catalog) = FakeLos.ImportedProject();
        var plc = project.Plcs[0];
        plc.Locations.Add(new Location { Name = "1000+FIX01", Number = 3 });
        plc.Locations.Add(new Location { Name = "1000+FIX02", Number = 4 });
        plc.Operations.Add(new Operation
        {
            Id = "110",
            Locations =
            [
                new OperationLocation
                {
                    Location = "1000+FIX01",
                    References =
                    [
                        new ReferenceSignals { Reference = 1, PartPresent = ["++1000+FIX01-PP01", "++1000+FIX01-PP02"], ClampsOpen = ["++1000+FIX01-YA-H"] },
                        new ReferenceSignals { Reference = 2, PartPresent = ["++1000+FIX01-PP01", "++1000+FIX01-PP02"] },
                    ],
                },
                new OperationLocation { Location = "1000+FIX02", VirtualLoc = "alwaysTrue", References = [new ReferenceSignals { Reference = 3 }] },
            ],
        });
        plc.FindAsset("LX9_01_1000FIX01_01_MOVEHR")!.Operation = "110";
        plc.FindAsset("LX9_01_1000FIX01_01_MOVEHR_02_FIX")!.Operation = "110";
        return (project, catalog, plc);
    }

    private static string Fresh(Plc plc, Catalog catalog) => FbGenerator.Generate(plc, catalog, null, new GenerateOptions()).Xml;

    private static List<string> Titles(string xml) => FbDocument.Parse(xml).Segments.Select(s => s.Title).ToList();

    [Fact]
    public void Desde_cero_el_FB_lleva_el_esqueleto_en_orden()
    {
        var (_, catalog, plc) = Prepared();

        var titles = Titles(Fresh(plc, catalog));

        Assert.Equal(
        [
            Segments.ConfigTitle,
            Segments.OperationsHeaderTitle,
            "Operation 110 :1000+FIX01, 1000+FIX02",
            Segments.CallsHeaderTitle,
            "LX9_01_CELL01 [LoS_CE_CELL]",
            "LX9_01_1000FIX01_01_MOVEHR [LoS_OP_MOVE_A]",
            "LX9_01_1000FIX01_01_MOVEHR_01_R [LoS_AS_ROBOT]",
            "LX9_01_1000FIX01_01_MOVEHR_02_FIX [LoS_AS_STATIC_FIXTURE]",
            "LX9_76_PLC01 [LoS_CE_PLC]",
            Segments.LifeBitTitle,
            Segments.Separator1Title,
            Segments.StructTitle,
            Segments.Separator2Title,
            Segments.SwapFloatTitle,
            Segments.SwapTitle,
        ], titles);
    }

    [Fact]
    public void La_estatica_lleva_devices_variables_instancias_y_operaciones()
    {
        var (_, catalog, plc) = Prepared();

        var doc = FbDocument.Parse(Fresh(plc, catalog));
        var members = doc.Section("Static").Elements(FbXml.Interface + "Member")
            .Select(m => ((string)m.Attribute("Name")!, (string)m.Attribute("Datatype")!)).ToList();

        Assert.Equal(("Device_11", "\"GSDML_Device11\""), members[0]);
        Assert.Equal(("Device_12", "\"GSDML_Device12\""), members[1]);
        Assert.Contains(("LX9_01_1000FIX01_01_MOVEHR_01_R", "\"LoS_AS_ROBOT_UDT\""), members);
        Assert.Contains(("01_1000FIX01_01_MOVEHR_01_R", "\"LoS_AS_ROBOT\""), members);
        Assert.Equal(("Operation_110", "\"I40_FixtureCfg_UDT\""), members[^1]);
        Assert.Contains(doc.Section("Temp").Elements(), m => (string?)m.Attribute("Name") == "LifeBit");
        Assert.Equal(Naming.FbName, doc.Name);
    }

    [Fact]
    public void Los_ID_son_unicos_y_los_UId_no_se_repiten_en_cada_segmento()
    {
        var (_, catalog, plc) = Prepared();
        var first = Fresh(plc, catalog);
        plc.Operations.Add(new Operation { Id = "120", Locations = [new OperationLocation { Location = "1000+FIX02" }] });
        plc.FindAsset("LX9_01_1000FIX01_01_MOVEHR_01_R")!.Operation = "120";

        var xml = FbGenerator.Generate(plc, catalog, AddManualSegment(first), new GenerateOptions
        {
            Parts = FbParts.All,
            Assets = plc.Assets.Select(a => a.Element).ToHashSet(),
        }).Xml;

        var root = XDocument.Parse(xml).Root!;
        var ids = root.Descendants().Select(e => (string?)e.Attribute("ID")).Where(i => i is not null).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.Equal(Enumerable.Range(0, ids.Count).Select(i => i.ToString("X")), ids);
        foreach (var source in root.Descendants("NetworkSource"))
        {
            // NameCon e IdentCon apuntan al UId de otro elemento: no son declaraciones.
            var uids = source.Descendants()
                .Where(e => e.Name.LocalName is not ("NameCon" or "IdentCon"))
                .Select(e => (string?)e.Attribute("UId")).Where(u => u is not null).ToList();
            Assert.Equal(uids.Count, uids.Distinct().Count());
            var declared = uids.ToHashSet();
            Assert.All(source.Descendants().Where(e => e.Name.LocalName is "NameCon" or "IdentCon"),
                c => Assert.Contains((string)c.Attribute("UId")!, declared));
        }
    }

    [Fact]
    public void Operacion_en_STL_con_mascara_senales_agrupadas_y_alwaysFalse()
    {
        var (_, catalog, plc) = Prepared();

        var stl = Stl(Fresh(plc, catalog), "Operation 110 :1000+FIX01, 1000+FIX02");

        Assert.Equal(
        [
            "L 2", "T Operation_110.Amount",
            "L 3", "T Operation_110.Location[1].Loc",
            "L 3", "T Operation_110.Location[1].Mask",
            "A alwaysFalse", "Assign Operation_110.Location[1].VirtualLoc",
            "A ++1000+FIX01-PP01", "A ++1000+FIX01-PP02",
            "Assign Operation_110.Location[1].PartPresent_ref[1]", "Assign Operation_110.Location[1].PartPresent_ref[2]",
            "A ++1000+FIX01-YA-H", "Assign Operation_110.Location[1].ClampsOpen_Ref[1]",
            "A alwaysFalse", "Assign Operation_110.Location[1].ClampsOpen_Ref[2]",
            "L 4", "T Operation_110.Location[2].Loc",
            "L 4", "T Operation_110.Location[2].Mask",
            "A alwaysTrue", "Assign Operation_110.Location[2].VirtualLoc",
            "A alwaysFalse", "Assign Operation_110.Location[2].PartPresent_ref[3]",
            "A alwaysFalse", "Assign Operation_110.Location[2].ClampsOpen_Ref[3]",
        ], stl);
    }

    [Fact]
    public void Struct_TO_DEVICE_por_regiones_y_SWAP_de_todos_los_enteros()
    {
        var (_, catalog, plc) = Prepared();
        var xml = Fresh(plc, catalog);

        var scl = Scl(xml, Segments.StructTitle);
        Assert.Contains("REGION General", scl);
        Assert.Contains("#Device_11.\"Bool_011_o(000)_04\" := #LX9_76_PLC01.PLCHeartbeat;", scl);
        Assert.DoesNotContain("NetworkAlarm", scl);
        Assert.Contains("REGION LX9_01_CELL01", scl);
        Assert.Contains("    REGION CELL01_Estado", scl);
        Assert.Contains("#Device_12.\"String40_012_o(020)\" := #LX9_01_CELL01.CellWorkingEvent;", scl);
        Assert.Contains("    REGION 1000FIX01_01_MOVEHR", scl);
        Assert.Contains("#Device_11.\"Int16_011_o(002)\" := #LX9_01_1000FIX01_01_MOVEHR_01_R.Job;", scl);

        var swap = Scl(xml, Segments.SwapTitle);
        Assert.Equal(5, swap.Split('\n').Count(l => l.Contains("SWAP")));
        Assert.Contains("#Device_11.\"Int16_011_o(003)\" := SWAP<Word>(#Device_11.\"Int16_011_o(003)\");", swap);
        Assert.Contains("SWAP<LWord>", swap);

        var floats = Segment(xml, Segments.SwapFloatTitle).Descendants(FbXml.FlgNet + "CallInfo").ToList();
        Assert.Equal(2, floats.Count);
        Assert.All(floats, c => Assert.Equal("Swap_Real", (string?)c.Attribute("Name")));
    }

    [Fact]
    public void Un_tipo_sin_FB_escribe_sus_variables_desde_el_origen_en_General()
    {
        var (_, catalog, plc) = Prepared();
        var type = catalog.Find("CE_PLC")!;
        type.Fb = "";
        type.Udt = "";
        type.NoFb = true;
        type.Variables.First(v => v.Name == "PLCHeartbeat").Source = "#LifeBit";

        var result = FbGenerator.Generate(plc, catalog, null, new GenerateOptions());
        var general = Scl(result.Xml, Segments.StructTitle).Split("END_REGION")[0];
        var statics = FbDocument.Parse(result.Xml).Section("Static").Elements().Select(m => (string?)m.Attribute("Name"));

        Assert.StartsWith("REGION General", general.Trim());
        Assert.Contains("#Device_11.\"Bool_011_o(000)_04\" := #LifeBit;", general);
        Assert.DoesNotContain(Titles(result.Xml), t => t.StartsWith("LX9_76_PLC01"));
        Assert.DoesNotContain("LX9_76_PLC01", statics);
        Assert.DoesNotContain(result.Warnings, w => w.Contains("LX9_76_PLC01"));

        type.Variables.First(v => v.Name == "PLCHeartbeat").Source = "\"Info_DB\".stat.\"Firma 1\"";
        var global = Segment(FbGenerator.Generate(plc, catalog, null, new GenerateOptions()).Xml, Segments.StructTitle)
            .Descendants(FbXml.Scl + "Access").Single(a => (string?)a.Attribute("Scope") == "GlobalVariable");
        Assert.Equal(["Info_DB", "stat", "Firma 1"], global.Descendants(FbXml.Scl + "Component").Select(c => (string)c.Attribute("Name")!));

        type.Variables.First(v => v.Name == "PLCHeartbeat").Source = "";
        Assert.DoesNotContain("Bool_011_o(000)_04", Scl(Fresh(plc, catalog), Segments.StructTitle));
    }

    [Fact]
    public void La_linea_de_un_tipo_sin_FB_se_anade_a_General_aunque_el_asset_no_este_marcado()
    {
        var (_, catalog, plc) = Prepared();
        var type = catalog.Find("CE_PLC")!;
        type.Fb = "";
        type.Udt = "";
        type.NoFb = true;
        // El FB actual se generó cuando la variable aún no tenía origen: General está vacía.
        var current = Fresh(plc, catalog);
        Assert.DoesNotContain("Bool_011_o(000)_04", Scl(current, Segments.StructTitle));
        type.Variables.First(v => v.Name == "PLCHeartbeat").Source = "#LifeBit";
        var line = "#Device_11.\"Bool_011_o(000)_04\" := #LifeBit;";

        foreach (var parts in new[] { FbParts.Struct, FbParts.None })
        {
            var xml = FbGenerator.Generate(plc, catalog, current, new GenerateOptions { Parts = parts }).Xml;
            var general = Scl(xml, Segments.StructTitle).Split("END_REGION")[0];
            Assert.Contains(line, general);

            // Si ya está, no se repite.
            var again = FbGenerator.Generate(plc, catalog, xml, new GenerateOptions { Parts = parts }).Xml;
            Assert.Single(Scl(again, Segments.StructTitle).Split('\n'), l => l.Contains(line));
        }
    }

    [Theory]
    [InlineData("#LifeBit", false, new[] { "LifeBit" })]
    [InlineData("#Device_11.\"Bool_011_o(000)_04\"", false, new[] { "Device_11", "Bool_011_o(000)_04" })]
    [InlineData("\"Info_DB\".stat.signature", true, new[] { "Info_DB", "stat", "signature" })]
    [InlineData("alwaysTrue", true, new[] { "alwaysTrue" })]
    public void Una_ruta_SCL_se_lee_por_componentes(string text, bool global, string[] components)
    {
        var path = SclPath.Parse(text)!;
        Assert.Equal(global, path.Global);
        Assert.Equal(components, path.Components);
    }

    [Theory]
    [InlineData("")]
    [InlineData("#")]
    [InlineData("#a..b")]
    [InlineData("#a[1]")]
    [InlineData("\"DB.a")]
    [InlineData("#a + 1")]
    public void Una_ruta_SCL_no_valida_no_se_lee(string text) => Assert.Null(SclPath.Parse(text));

    [Fact]
    public void Un_asset_sin_equivalencia_no_se_genera_y_se_avisa()
    {
        var (_, catalog, plc) = Prepared();
        catalog.Find("AS_ROBOT")!.Udt = "";

        var result = FbGenerator.Generate(plc, catalog, null, new GenerateOptions());

        Assert.DoesNotContain(Titles(result.Xml), t => t.StartsWith("LX9_01_1000FIX01_01_MOVEHR_01_R"));
        Assert.Contains(result.Warnings, w => w.Contains("LX9_01_1000FIX01_01_MOVEHR_01_R"));
        Assert.DoesNotContain("MOVEHR_01_R.Job", Scl(result.Xml, Segments.StructTitle));
    }

    [Fact]
    public void Regenerar_conserva_lo_manual_y_solo_reescribe_lo_marcado()
    {
        var (_, catalog, plc) = Prepared();
        var current = AddManualSegment(Fresh(plc, catalog));
        // El usuario cambió a mano la operación 110 en TIA: sin marcar, se respeta.
        current = current.Replace("<ConstantValue>3</ConstantValue>", "<ConstantValue>9</ConstantValue>");
        plc.Assets.Add(new Asset { Element = "LX9_01_1000FIX01_01_MOVEHR_03_R", Type = "AS_ROBOT", Kind = AssetKind.Asset, Manual = true });

        var xml = FbGenerator.Generate(plc, catalog, current, new GenerateOptions()).Xml;
        var titles = Titles(xml);

        Assert.Contains("Segmento manual", titles);
        Assert.Equal(titles.IndexOf("LX9_01_1000FIX01_01_MOVEHR_02_FIX [LoS_AS_STATIC_FIXTURE]") + 1,
            titles.IndexOf("LX9_01_1000FIX01_01_MOVEHR_03_R [LoS_AS_ROBOT]"));
        Assert.Contains("L 9", Stl(xml, "Operation 110 :1000+FIX01, 1000+FIX02"));

        var marked = FbGenerator.Generate(plc, catalog, current, new GenerateOptions { Parts = FbParts.Operations }).Xml;
        Assert.DoesNotContain("L 9", Stl(marked, "Operation 110 :1000+FIX01, 1000+FIX02"));
        Assert.Contains("Segmento manual", Titles(marked));
    }

    [Fact]
    public void Regenerar_una_llamada_conserva_la_logica_cableada_a_mano()
    {
        var (_, catalog, plc) = Prepared();
        var robot = catalog.Find("AS_ROBOT")!;
        robot.Parameters = [new("NumberRobot", "Input", "Int"), new("AS_ROBOT", "Output", "\"LoS_AS_ROBOT_UDT\"")];
        var current = WireConstant(Fresh(plc, catalog), "LX9_01_1000FIX01_01_MOVEHR_01_R", "NumberRobot", "7");
        plc.FindAsset("LX9_01_1000FIX01_01_MOVEHR_01_R")!.Operation = "110";
        robot.Fb = "LoS_AS_ROBOT_V2";
        robot.Parameters = [new("NumberRobot", "Input", "Int"), new("Operation", "Input", "\"I40_FixtureCfg_UDT\""), new("AS_ROBOT", "Output", "\"LoS_AS_ROBOT_UDT\"")];

        var xml = FbGenerator.Generate(plc, catalog, current, new GenerateOptions
        {
            Parts = FbParts.Calls,
            Assets = ["LX9_01_1000FIX01_01_MOVEHR_01_R"],
        }).Xml;

        var segment = Segment(xml, "LX9_01_1000FIX01_01_MOVEHR_01_R [LoS_AS_ROBOT_V2]");
        var read = FbReader.Read(xml).Calls.Single(c => c.Element == "LX9_01_1000FIX01_01_MOVEHR_01_R");
        Assert.Equal("LoS_AS_ROBOT_V2", read.Fb);
        Assert.Equal("110", read.Operation);
        Assert.Equal("AS_ROBOT", read.UdtPin);
        Assert.Contains(segment.Descendants(FbXml.FlgNet + "ConstantValue"), c => c.Value == "7");
    }

    [Fact]
    public void Cada_pin_declarado_en_una_llamada_tiene_una_conexion()
    {
        var (_, catalog, plc) = Prepared();
        var robot = catalog.Find("AS_ROBOT")!;
        robot.Parameters = [new("NumberRobot", "Input", "Int"), new("Operation", "Input", "\"I40_FixtureCfg_UDT\""),
            new("Busy", "Output", "Bool"), new("AS_ROBOT", "Output", "\"LoS_AS_ROBOT_UDT\"")];
        var fresh = Fresh(plc, catalog);
        var current = WireConstant(fresh, "LX9_01_1000FIX01_01_MOVEHR_01_R", "NumberRobot", "7");
        plc.FindAsset("LX9_01_1000FIX01_01_MOVEHR_01_R")!.Operation = "110";
        var regenerated = FbGenerator.Generate(plc, catalog, current, new GenerateOptions
        {
            Parts = FbParts.Calls,
            Assets = ["LX9_01_1000FIX01_01_MOVEHR_01_R"],
        }).Xml;

        foreach (var xml in new[] { fresh, regenerated })
        {
            var net = FbXml.Source(FbDocument.Parse(xml).Find(SegmentKeys.Call("LX9_01_1000FIX01_01_MOVEHR_01_R"))!.Unit)!;
            var ns = FbXml.FlgNet;
            var callId = (string)net.Descendants(ns + "Call").First().Attribute("UId")!;
            var connections = net.Descendants(ns + "NameCon").Where(n => (string?)n.Attribute("UId") == callId)
                .Select(n => (string)n.Attribute("Name")!).ToList();
            var declared = net.Descendants(ns + "Parameter").Select(p => (string)p.Attribute("Name")!).ToList();
            Assert.Equal(4, declared.Count);
            Assert.Single(connections, "en");
            Assert.Equal(declared.Order(), connections.Where(c => c != "en").Order());
        }
    }

    [Fact]
    public void Si_el_FB_nuevo_pierde_un_pin_cableado_no_se_regenera_sin_confirmar()
    {
        var (_, catalog, plc) = Prepared();
        var robot = catalog.Find("AS_ROBOT")!;
        robot.Parameters = [new("NumberRobot", "Input", "Int"), new("AS_ROBOT", "Output", "\"LoS_AS_ROBOT_UDT\"")];
        var current = WireConstant(Fresh(plc, catalog), "LX9_01_1000FIX01_01_MOVEHR_01_R", "NumberRobot", "7");
        robot.Fb = "LoS_AS_ROBOT_V2";
        robot.Parameters = [new("AS_ROBOT", "Output", "\"LoS_AS_ROBOT_UDT\"")];
        var options = new GenerateOptions { Parts = FbParts.Calls, Assets = ["LX9_01_1000FIX01_01_MOVEHR_01_R"] };

        var result = FbGenerator.Generate(plc, catalog, current, options);
        var loss = Assert.Single(result.PinLosses);
        Assert.Equal(["NumberRobot"], loss.Pins);
        Assert.Contains("LX9_01_1000FIX01_01_MOVEHR_01_R [LoS_AS_ROBOT]", Titles(result.Xml));

        options.ConfirmedPinLoss.Add("LX9_01_1000FIX01_01_MOVEHR_01_R");
        var confirmed = FbGenerator.Generate(plc, catalog, current, options);
        Assert.Empty(confirmed.PinLosses);
        Assert.Contains("LX9_01_1000FIX01_01_MOVEHR_01_R [LoS_AS_ROBOT_V2]", Titles(confirmed.Xml));
    }

    [Fact]
    public void Struct_TO_DEVICE_conserva_las_regiones_hechas_a_mano()
    {
        var (_, catalog, plc) = Prepared();
        var fresh = Fresh(plc, catalog);
        var doc = FbDocument.Parse(fresh);
        var source = FbXml.Source(doc.Find(SegmentKeys.Struct)!.Unit)!;
        // Región manual dentro de la celda, detrás de la del asset de operación.
        var end = source.Elements(FbXml.Scl + "Token").Last(t => (string?)t.Attribute("Text") == "END_REGION");
        end.AddBeforeSelf(
            new XElement(FbXml.Scl + "Token", new XAttribute("Text", "REGION")),
            new XElement(FbXml.Scl + "Blank", new XAttribute("Num", 1)),
            new XElement(FbXml.Scl + "Text", "Reglas_manuales"),
            new XElement(FbXml.Scl + "NewLine", new XAttribute("Num", 1)),
            new XElement(FbXml.Scl + "Token", new XAttribute("Text", "END_REGION")),
            new XElement(FbXml.Scl + "NewLine", new XAttribute("Num", 1)));
        var current = doc.Write();
        plc.FindAsset("LX9_01_CELL01")!.Variables.RemoveAt(1);

        var xml = FbGenerator.Generate(plc, catalog, current, new GenerateOptions
        {
            Parts = FbParts.Struct,
            Assets = ["LX9_01_CELL01"],
        }).Xml;

        var scl = Scl(xml, Segments.StructTitle);
        Assert.Contains("REGION Reglas_manuales", scl);
        Assert.DoesNotContain("DoorOpen01", scl);
        Assert.Contains("REGION 1000FIX01_01_MOVEHR", scl);
    }

    [Fact]
    public void Leer_FB_recupera_operaciones_locations_y_asignaciones()
    {
        var (project, catalog, plc) = Prepared();
        var xml = Fresh(plc, catalog);
        var expected = Storage.ToJson(plc.Operations);
        var clean = Storage.Clone(project).Plcs[0];
        clean.Operations.Clear();
        clean.Locations.Clear();
        foreach (var asset in clean.Assets)
        {
            asset.Operation = null;
        }

        var sync = FbSyncPlan.Create(clean, catalog, xml, "fb.xml");
        Assert.Contains(sync.Changes, c => c.Contains("110"));
        sync.Apply();

        Assert.Equal(expected, Storage.ToJson(clean.Operations));
        Assert.Equal(3, clean.FindLocation("1000+FIX01")!.Number);
        Assert.Equal("110", clean.FindAsset("LX9_01_1000FIX01_01_MOVEHR_02_FIX")!.Operation);
        Assert.Equal(xml, clean.CurrentFb);
    }

    [Fact]
    public void Leer_FB_aprende_la_equivalencia_de_un_tipo_que_no_la_tiene()
    {
        var (_, catalog, plc) = Prepared();
        var xml = Fresh(plc, catalog);
        var empty = new Catalog { Types = catalog.Types.Select(t => new AssetType { Name = t.Name }).ToList() };

        FbSyncPlan.Create(plc, empty, xml).Apply();

        var robot = empty.Find("AS_ROBOT")!;
        Assert.True(robot.HasEquivalence);
        Assert.Equal("LoS_AS_ROBOT", robot.Fb);
        Assert.Equal("LoS_AS_ROBOT_UDT", robot.Udt);
        Assert.Equal("AS_ROBOT", robot.UdtPin);
    }

    // ---------------------------------------------------------------- utilidades

    private static XElement Segment(string xml, string title) =>
        FbDocument.Parse(xml).Segments.First(s => s.Title == title).Unit;

    // STL como texto: «L 3», «T Operation_110.Amount»...
    private static List<string> Stl(string xml, string title)
    {
        var ns = FbXml.Stl;
        return FbXml.Source(Segment(xml, title))!.Elements(ns + "StlStatement")
            .Select(s =>
            {
                var token = (string)s.Element(ns + "StlToken")!.Attribute("Text")!;
                var access = s.Element(ns + "Access");
                if (access is null)
                {
                    return token;
                }
                if ((string?)access.Attribute("Scope") == "LiteralConstant")
                {
                    return token + " " + access.Descendants(ns + "ConstantValue").First().Value;
                }
                var path = access.Element(ns + "Symbol")!.Elements(ns + "Component").Select(c =>
                    (string)c.Attribute("Name")! + (c.Descendants(ns + "ConstantValue").FirstOrDefault() is { } i ? $"[{i.Value}]" : ""));
                return token + " " + string.Join(".", path);
            })
            .Where(t => t is not ("COMMENT" or "EMPTY_LINE"))
            .ToList();
    }

    // SCL como texto, para comprobarlo cómodamente.
    private static string Scl(string xml, string title)
    {
        var ns = FbXml.Scl;
        var sb = new System.Text.StringBuilder();
        void Walk(XElement e)
        {
            foreach (var c in e.Elements())
            {
                switch (c.Name.LocalName)
                {
                    case "Token": sb.Append((string)c.Attribute("Text")!); break;
                    case "Blank": sb.Append(' ', (int)c.Attribute("Num")!); break;
                    case "NewLine": sb.Append('\n', (int)c.Attribute("Num")!); break;
                    case "Text": sb.Append(c.Value); break;
                    case "Component":
                        var name = (string)c.Attribute("Name")!;
                        sb.Append(System.Text.RegularExpressions.Regex.IsMatch(name, @"^[A-Za-z_]\w*$") ? name : $"\"{name}\"");
                        break;
                    case "Access" when (string?)c.Attribute("Scope") == "LocalVariable": sb.Append('#'); Walk(c); break;
                    case "Instruction": sb.Append((string)c.Attribute("Name")!); Walk(c); break;
                    case "TemplateValue": sb.Append('<').Append(c.Value).Append('>'); break;
                    default: Walk(c); break;
                }
            }
        }
        Walk(FbXml.Source(Segment(xml, title))!);
        return sb.ToString();
    }

    private static string AddManualSegment(string xml)
    {
        var doc = FbDocument.Parse(xml);
        var at = doc.Segments.FindIndex(s => s.Key == SegmentKeys.LifeBit);
        doc.Segments.Insert(at, new FbSegment(Segments.Empty("Segmento manual", doc.Cultures), null));
        return doc.Write();
    }

    // Simula lógica cableada a mano: una constante conectada a un pin de la llamada.
    private static string WireConstant(string xml, string element, string pin, string value)
    {
        var doc = FbDocument.Parse(xml);
        var unit = doc.Find(SegmentKeys.Call(element))!.Unit;
        var net = FbXml.Source(unit)!;
        var ns = FbXml.FlgNet;
        var callId = (string)net.Descendants(ns + "Call").First().Attribute("UId")!;
        // Como en TIA: el cable sustituye al OpenCon del pin.
        net.Element(ns + "Wires")!.Elements(ns + "Wire")
            .Where(w => w.Element(ns + "OpenCon") is not null
                && w.Elements(ns + "NameCon").Any(n => (string?)n.Attribute("UId") == callId && (string?)n.Attribute("Name") == pin))
            .Remove();
        net.Element(ns + "Parts")!.AddFirst(new XElement(ns + "Access", new XAttribute("Scope", "LiteralConstant"), new XAttribute("UId", "90"),
            new XElement(ns + "Constant", new XElement(ns + "ConstantType", "Int"), new XElement(ns + "ConstantValue", value))));
        net.Element(ns + "Wires")!.Add(new XElement(ns + "Wire", new XAttribute("UId", "91"),
            new XElement(ns + "IdentCon", new XAttribute("UId", "90")), new XElement(ns + "NameCon", new XAttribute("UId", callId), new XAttribute("Name", pin))));
        return doc.Write();
    }
}
