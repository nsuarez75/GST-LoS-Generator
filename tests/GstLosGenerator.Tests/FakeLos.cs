// LoS inventada para los tests, con la misma forma que una real (hoja LoS y hojas «PLCxx - OUT»).
// Ningún dato sale de una LoS de fábrica.
using ClosedXML.Excel;
using GstLosGenerator.Core;

namespace GstLosGenerator.Tests;

internal sealed record FakeSignal(string Plc, string Mark, string Element, string Signal, string Template, string DataType, string Direction, string Address);

internal static class FakeLos
{
    public static List<FakeSignal> DefaultSignals() =>
    [
        new("PLC01", "C", "LX9_01_CELL01", "ProductionStatus", "CE_CELL", "Bool", "OUT", "Bool_011_o(000)_00"),
        new("PLC01", "", "LX9_01_CELL01", "DoorOpen01", "CE_CELL", "Bool", "OUT", "Bool_011_o(000)_01"),
        new("PLC01", "", "LX9_01_CELL01", "CellWorkingEvent", "CE_CELL", "String40", "OUT", "String_012_o(020)"),
        new("PLC01", "O", "LX9_01_1000FIX01_01_MOVEHR", "PartPresence01", "OP_MOVE_A", "Bool", "OUT", "Bool_011_o(000)_02"),
        new("PLC01", "", "LX9_01_1000FIX01_01_MOVEHR", "PartReference01", "OP_MOVE_A", "Byte", "OUT", "Byte_011_o(001)_00"),
        new("PLC01", "", "LX9_01_1000FIX01_01_MOVEHR", "TraceabilityCode01", "OP_MOVE_A", "Int64", "OUT", "Int64_011_o(010)"),
        new("PLC01", "A", "LX9_01_1000FIX01_01_MOVEHR_01_R", "Job", "AS_ROBOT", "Int16", "OUT", "Int16_011_o(002)"),
        new("PLC01", "", "LX9_01_1000FIX01_01_MOVEHR_01_R", "Torque", "AS_ROBOT", "Float", "OUT", "Float_011_o(006)"),
        new("PLC01", "A", "LX9_01_1000FIX01_01_MOVEHR_02_FIX", "ClampsOpen", "AS_STATIC_FIXTURE", "Bool", "OUT", "Bool_011_o(000)_03"),
        new("PLC01", "", "LX9_01_1000FIX01_01_MOVEHR_02_FIX", "Cycles", "AS_STATIC_FIXTURE", "Int32", "OUT", "Int32_011_o(004)"),
        new("PLC01", "", "LX9_76_PLC01", "PLCHeartbeat", "CE_PLC", "Bool", "OUT", "Bool_011_o(000)_04"),
        new("PLC01", "", "LX9_76_PLC01", "NetworkAlarm", "CE_PLC", "Int64", "IN", "Int64_011_i(014)"),
        new("PLC02", "C", "LX9_02_CELL02", "ProductionStatus", "CE_CELL", "Bool", "OUT", "Bool_021_o(000)_00"),
    ];

    // Direcciones de cada device: (PLC, device, dirección, tipo cuando cambia).
    public static List<(string Plc, int Device, string Address, string Type)> DefaultAddresses() =>
    [
        ("PLC01", 1, "Bool_011_o(000)_00", "Bool"),
        ("PLC01", 1, "Bool_011_o(000)_01", ""),
        ("PLC01", 1, "Bool_011_o(000)_02", ""),
        ("PLC01", 1, "Bool_011_o(000)_03", ""),
        ("PLC01", 1, "Bool_011_o(000)_04", ""),
        ("PLC01", 1, "Byte_011_o(001)_00", "Byte"),
        ("PLC01", 1, "Int16_011_o(002)", "Int16"),
        ("PLC01", 1, "Int16_011_o(003)", ""),
        ("PLC01", 1, "Int32_011_o(004)", "Int32"),
        ("PLC01", 1, "Float_011_o(006)", "Float"),
        ("PLC01", 1, "Float_011_o(008)", ""),
        ("PLC01", 1, "Int64_011_o(010)", "Int64"),
        ("PLC01", 1, "Int64_011_o(014)", "END"),
        ("PLC01", 2, "String_012_o(000)", "String"),
        ("PLC01", 2, "String_012_o(020)", "String40"),
        ("PLC02", 1, "Bool_021_o(000)_00", "Bool"),
    ];

    public static MemoryStream Build(List<FakeSignal>? signals = null, List<(string Plc, int Device, string Address, string Type)>? addresses = null)
    {
        signals ??= DefaultSignals();
        addresses ??= DefaultAddresses();
        using var workbook = new XLWorkbook();
        var los = workbook.AddWorksheet("LoS");
        string[] header = ["Source", "C", "O", "A/SA", "Element", "SignalName", "LayoutName", "TC digit 4, 5", "Description", "Template", "PLCDataType", "Bytes", "InputOutput", "IPC signal name"];
        for (var c = 0; c < header.Length; c++)
        {
            los.Cell(1, c + 1).Value = header[c];
        }
        var r = 2;
        foreach (var s in signals)
        {
            los.Cell(r, 1).Value = s.Plc;
            if (s.Mark.Length > 0)
            {
                los.Cell(r, s.Mark switch { "C" => 2, "O" => 3, _ => 4 }).Value = "X";
            }
            los.Cell(r, 5).Value = s.Element;
            los.Cell(r, 6).Value = s.Signal;
            los.Cell(r, 7).Value = "LAYOUT";
            los.Cell(r, 9).Value = "Descripción de " + s.Signal;
            los.Cell(r, 10).Value = s.Template;
            los.Cell(r, 11).Value = s.DataType;
            los.Cell(r, 13).Value = s.Direction;
            los.Cell(r, 14).Value = s.Address;
            r++;
        }
        foreach (var plc in addresses.GroupBy(a => a.Plc))
        {
            var sheet = workbook.AddWorksheet($"{plc.Key} - OUT");
            var row = 1;
            foreach (var device in plc.GroupBy(a => a.Device))
            {
                sheet.Cell(row++, 1).Value = $"DEVICE {device.Key}";
                string[] cols = ["Element.SignalName", "PLC", "DEVICE", "Input/Output", "WORD", "BIT", "Address"];
                for (var c = 0; c < cols.Length; c++)
                {
                    sheet.Cell(row, c + 1).Value = cols[c];
                }
                row++;
                foreach (var a in device)
                {
                    sheet.Cell(row, 2).Value = Naming.PlcNumber(plc.Key);
                    sheet.Cell(row, 3).Value = device.Key;
                    sheet.Cell(row, 7).Value = a.Address;
                    if (a.Type.Length > 0)
                    {
                        sheet.Cell(row, 8).Value = a.Type;
                    }
                    row++;
                }
                row += 2;
            }
        }
        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }

    public static LosData Read(List<FakeSignal>? signals = null) => LosReader.Read(Build(signals));

    // Proyecto con la LoS inventada ya importada y el catálogo con equivalencias completas.
    public static (Project Project, Catalog Catalog) ImportedProject()
    {
        var project = new Project { Name = "Prueba" };
        var catalog = new Catalog();
        LosImportPlan.Create(project, catalog, Read()).Apply();
        foreach (var type in catalog.Types)
        {
            type.Fb = "LoS_" + type.Name;
            type.Udt = "LoS_" + type.Name + "_UDT";
            type.UdtPin = type.Name;
        }
        return (project, catalog);
    }
}
