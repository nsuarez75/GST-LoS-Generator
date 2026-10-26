// Lectura de la LoS (Excel): la hoja LoS da los assets y sus variables;
// las hojas «PLCxx - OUT» dan los devices de cada PLC con todas sus direcciones.
using ClosedXML.Excel;

namespace GstLosGenerator.Core;

public sealed class LosData
{
    public List<LosPlc> Plcs { get; } = [];
}

public sealed class LosPlc
{
    public string Name { get; init; } = "";
    public int Number { get; init; }
    public List<Device> Devices { get; } = [];
    public List<Asset> Assets { get; } = [];
}

public sealed class LosFormatException(string message) : Exception(message);

public static class LosReader
{
    public const string SignalsSheet = "LoS";

    public static LosData Read(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        return Read(stream);
    }

    public static LosData Read(Stream stream)
    {
        using var workbook = new XLWorkbook(stream);
        if (!workbook.TryGetWorksheet(SignalsSheet, out var signals))
        {
            throw new LosFormatException(L.T("La LoS no tiene la hoja «{0}».", SignalsSheet));
        }
        var data = new LosData();
        ReadSignals(signals, data);
        foreach (var plc in data.Plcs)
        {
            if (workbook.TryGetWorksheet($"{plc.Name} - OUT", out var sheet))
            {
                plc.Devices.AddRange(ReadDevices(sheet));
            }
        }
        return data;
    }

    private static void ReadSignals(IXLWorksheet sheet, LosData data)
    {
        var header = sheet.Row(1);
        var columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var cell in header.CellsUsed())
        {
            columns.TryAdd(cell.GetString().Trim(), cell.Address.ColumnNumber);
        }
        int Col(string name) => columns.TryGetValue(name, out var c)
            ? c
            : throw new LosFormatException(L.T("A la hoja «{0}» le falta la columna «{1}».", SignalsSheet, name));
        int? OptionalCol(string name) => columns.TryGetValue(name, out var c) ? c : null;

        var source = Col("Source");
        var element = Col("Element");
        var signal = Col("SignalName");
        var template = Col("Template");
        var dataType = Col("PLCDataType");
        var direction = Col("InputOutput");
        var address = Col("IPC signal name");
        var layout = OptionalCol("LayoutName");
        var description = OptionalCol("Description");
        var cellMark = OptionalCol("C");
        var operationMark = OptionalCol("O");
        var assetMark = OptionalCol("A/SA");

        var plcs = new Dictionary<string, LosPlc>();
        var assets = new Dictionary<(string, string), Asset>();
        var last = sheet.LastRowUsed()?.RowNumber() ?? 1;
        for (var r = 2; r <= last; r++)
        {
            var row = sheet.Row(r);
            string Text(int c) => row.Cell(c).GetString().Trim();
            string OptionalText(int? c) => c is null ? "" : Text(c.Value);

            var plcName = Text(source);
            var elementName = Text(element);
            if (plcName.Length == 0 || elementName.Length == 0)
            {
                continue;
            }
            if (!plcs.TryGetValue(plcName, out var plc))
            {
                plc = new LosPlc { Name = plcName, Number = Naming.PlcNumber(plcName) };
                plcs[plcName] = plc;
                data.Plcs.Add(plc);
            }
            if (!assets.TryGetValue((plcName, elementName), out var asset))
            {
                asset = new Asset
                {
                    Element = elementName,
                    Type = Text(template),
                    LayoutName = OptionalText(layout),
                    Kind = AssetKind.Other,
                };
                assets[(plcName, elementName)] = asset;
                plc.Assets.Add(asset);
            }
            if (asset.Kind == AssetKind.Other)
            {
                asset.Kind = IsMarked(OptionalText(cellMark)) ? AssetKind.Cell
                    : IsMarked(OptionalText(operationMark)) ? AssetKind.Operation
                    : IsMarked(OptionalText(assetMark)) ? AssetKind.Asset
                    : AssetKind.Other;
            }
            asset.Variables.Add(new AssetVariable
            {
                Name = Text(signal),
                DataType = Text(dataType),
                Direction = Text(direction).ToUpperInvariant(),
                Address = Text(address),
                Description = OptionalText(description),
            });
        }
    }

    private static bool IsMarked(string text) => text.Length > 0;

    // Cada bloque empieza con «DEVICE n», sigue la cabecera y después una dirección por fila.
    // La columna que hay a la derecha de Address solo trae el tipo cuando cambia, y END en la última.
    internal static List<Device> ReadDevices(IXLWorksheet sheet)
    {
        var devices = new List<Device>();
        Device? current = null;
        var addressColumn = 0;
        var type = "";
        var last = sheet.LastRowUsed()?.RowNumber() ?? 0;
        for (var r = 1; r <= last; r++)
        {
            var row = sheet.Row(r);
            var first = row.Cell(1).GetString().Trim();
            if (first.StartsWith("DEVICE", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(first[6..].Trim(), out var number))
            {
                current = new Device { Number = number };
                devices.Add(current);
                addressColumn = 0;
                type = "";
                continue;
            }
            if (current is null)
            {
                continue;
            }
            if (addressColumn == 0)
            {
                var header = row.CellsUsed().FirstOrDefault(c => c.GetString().Trim().Equals("Address", StringComparison.OrdinalIgnoreCase));
                if (header is not null)
                {
                    addressColumn = header.Address.ColumnNumber;
                }
                continue;
            }
            var address = row.Cell(addressColumn).GetString().Trim();
            if (address.Length == 0)
            {
                continue;
            }
            var marker = row.Cell(addressColumn + 1).GetString().Trim();
            if (marker.Length > 0 && !marker.Equals("END", StringComparison.OrdinalIgnoreCase))
            {
                type = marker;
            }
            if (!Naming.TryParseAddress(address, out var parts))
            {
                continue;
            }
            var dataType = type.Length > 0 && type.StartsWith(parts.Type, StringComparison.Ordinal) ? type : parts.Type;
            current.Addresses.Add(new DeviceAddress
            {
                Name = Naming.WithType(address, dataType),
                DataType = dataType,
                SheetName = address,
            });
        }
        return devices;
    }
}
