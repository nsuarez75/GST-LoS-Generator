// Reglas de nombres compartidas por la importación de la LoS y la generación del FB.
using System.Globalization;
using System.Text.RegularExpressions;

namespace GstLosGenerator.Core;

public static partial class Naming
{
    public const string FbName = "User_LoS_Device_In_FB";
    public const string OperationUdt = "I40_FixtureCfg_UDT";
    public const string AlwaysFalse = "alwaysFalse";

    // PLC06, dispositivo 1 -> Device_61; PLC1, dispositivo 1 -> Device_11.
    public static string DeviceName(int plc, int device) => $"Device_{plc}{device}";

    public static string DeviceUdt(int plc, int device) => $"GSDML_Device{plc}{device}";

    // Número de PLC a partir de su nombre en la LoS: PLC06 -> 6.
    public static int PlcNumber(string plcName)
    {
        var m = DigitsAtEnd().Match(plcName);
        return m.Success ? int.Parse(m.Value, CultureInfo.InvariantCulture) : 0;
    }

    // Prefijo de línea: lo que va hasta el primer _ (LX9_).
    public static string WithoutLinePrefix(string element)
    {
        var i = element.IndexOf('_');
        return i < 0 ? element : element[(i + 1)..];
    }

    // Prefijo de línea y celda: LX9_01_.
    public static string CellPrefix(string element)
    {
        var parts = element.Split('_');
        return parts.Length >= 3 ? parts[0] + "_" + parts[1] + "_" : "";
    }

    // Nombre de la región de un asset de operación dentro de su celda: 1000CNV01_01_MOVECONV.
    public static string OperationRegion(string element)
    {
        var prefix = CellPrefix(element);
        return prefix.Length > 0 && element.StartsWith(prefix, StringComparison.Ordinal) ? element[prefix.Length..] : element;
    }

    // Región con las señales propias de una celda: LX9_01_CELL01 -> CELL01_Estado.
    public static string CellStateRegion(string cellElement) => OperationRegion(cellElement) + "_Estado";

    // Partes de una dirección: Bool_061_o(000)_00 -> tipo Bool, PLC 6, device 1, sentido o.
    public static bool TryParseAddress(string address, out AddressParts parts)
    {
        var m = AddressPattern().Match(address);
        if (!m.Success)
        {
            parts = default;
            return false;
        }
        var digits = m.Groups["dev"].Value;
        parts = new AddressParts(
            m.Groups["type"].Value,
            int.Parse(digits[..^1], CultureInfo.InvariantCulture),
            int.Parse(digits[^1..], CultureInfo.InvariantCulture),
            m.Groups["dir"].Value,
            m.Groups["rest"].Value);
        return true;
    }

    // Cambia el tipo de una dirección: String_062_o(524) con String120 -> String120_062_o(524).
    public static string WithType(string address, string dataType)
    {
        var i = address.IndexOf('_');
        return i < 0 ? address : dataType + address[i..];
    }

    [GeneratedRegex(@"\d+$")]
    private static partial Regex DigitsAtEnd();

    [GeneratedRegex(@"^(?<type>[A-Za-z]+\d*)_(?<dev>\d{2,})_(?<dir>[io])(?<rest>\(.*)$")]
    private static partial Regex AddressPattern();
}

public readonly record struct AddressParts(string Type, int Plc, int Device, string Direction, string Rest);

// Tipos de dato de la LoS y su equivalente en TIA.
public static partial class DataTypes
{
    public static string ToTia(string losType)
    {
        switch (losType)
        {
            case "Bool": return "Bool";
            case "Byte": return "Byte";
            case "Int16": return "Int";
            case "Int32": return "DInt";
            case "Int64": return "ULInt";
            case "Float": return "Real";
            case "String": return "Array[1..20] of Char";
        }
        var m = StringWithLength().Match(losType);
        return m.Success ? $"Array[1..{m.Groups[1].Value}] of Char" : losType;
    }

    // Tipo de SWAP para cada tipo de dato; null si no lleva SWAP en SCL.
    public static string? SwapType(string losType) => losType switch
    {
        "Int16" => "Word",
        "Int32" => "DWord",
        "Int64" => "LWord",
        _ => null,
    };

    public static bool IsFloat(string losType) => losType == "Float";

    [GeneratedRegex(@"^String(\d+)$")]
    private static partial Regex StringWithLength();
}
