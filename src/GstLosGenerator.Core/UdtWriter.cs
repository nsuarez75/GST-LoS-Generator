// UDT de un device en formato de fuente externa de TIA (.udt), igual que los exportados:
// UTF-8 con BOM, CRLF y sangría de tres espacios.
using System.Text;

namespace GstLosGenerator.Core;

public static class UdtWriter
{
    public static string FileName(Plc plc, Device device) => Naming.DeviceUdt(plc.Number, device.Number) + ".udt";

    public static string Write(Plc plc, Device device)
    {
        var sb = new StringBuilder();
        sb.Append("TYPE \"").Append(Naming.DeviceUdt(plc.Number, device.Number)).Append("\"\r\n");
        sb.Append("VERSION : 0.1\r\n");
        sb.Append("   STRUCT\r\n");
        foreach (var address in device.Addresses)
        {
            sb.Append("      \"").Append(address.Name).Append("\" : ").Append(DataTypes.ToTia(address.DataType)).Append(";\r\n");
        }
        sb.Append("   END_STRUCT;\r\n\r\nEND_TYPE\r\n\r\n");
        return sb.ToString();
    }

    public static void Save(Plc plc, Device device, string folder) =>
        File.WriteAllText(Path.Combine(folder, FileName(plc, device)), Write(plc, device), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
}
