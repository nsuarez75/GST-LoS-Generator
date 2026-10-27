// Modelo de datos de un proyecto y del catálogo de tipos. Es lo que se guarda en .losproj y .loscat.
using System.Text.Json.Serialization;

namespace GstLosGenerator.Core;

public sealed class Project
{
    public string Name { get; set; } = "";
    public List<Plc> Plcs { get; set; } = [];
    // Carpeta donde se escriben el FB y los UDT; se recuerda por proyecto.
    public string? OutputFolder { get; set; }
    // Nombre del archivo de la última LoS importada, solo informativo.
    public string? LosFile { get; set; }

    public Plc? FindPlc(string name) => Plcs.FirstOrDefault(p => p.Name == name);
}

public sealed class Plc
{
    // Nombre tal como aparece en la columna Source de la LoS (PLC06).
    public string Name { get; set; } = "";
    // Número que va en los nombres de device y direcciones (6 en PLC06).
    public int Number { get; set; }
    public List<Device> Devices { get; set; } = [];
    public List<Asset> Assets { get; set; } = [];
    public List<Location> Locations { get; set; } = [];
    public List<Operation> Operations { get; set; } = [];
    // FB tal como está en TIA, leído con «Leer FB». Es la base de la siguiente generación.
    public string? CurrentFb { get; set; }
    public string? CurrentFbFile { get; set; }

    public Asset? FindAsset(string element) => Assets.FirstOrDefault(a => a.Element == element);
    public Device? FindDevice(int number) => Devices.FirstOrDefault(d => d.Number == number);
    public Operation? FindOperation(string id) => Operations.FirstOrDefault(o => o.Id == id);
    public Location? FindLocation(string name) => Locations.FirstOrDefault(l => l.Name == name);

    public string DeviceName(int device) => Naming.DeviceName(Number, device);
}

public sealed class Device
{
    public int Number { get; set; }
    // Todas las direcciones de la hoja del PLC, en orden, se usen o no.
    public List<DeviceAddress> Addresses { get; set; } = [];
}

public sealed class DeviceAddress
{
    // Nombre del miembro en el UDT, con el tipo completo: String120_062_o(524).
    public string Name { get; set; } = "";
    // Tipo de dato de la LoS: Bool, Byte, Int16, Int32, Int64, Float, String, String40...
    public string DataType { get; set; } = "";
    // Nombre tal como lo escribe la LoS (String_062_o(524)); coincide con Name salvo en los String.
    public string SheetName { get; set; } = "";
}

public enum AssetKind
{
    // Activo de PLC (CE_PLC) u otro sin marca en la LoS.
    Other,
    Cell,
    Operation,
    Asset,
}

public sealed class Asset
{
    // Nombre completo del elemento: LX9_01_1000FIX01_01_MOVEHR.
    public string Element { get; set; } = "";
    // Tipo (columna Template de la LoS), que el usuario puede cambiar a mano.
    public string Type { get; set; } = "";
    // Tipo que trae la LoS; si Type es distinto, es que se cambió a mano y una LoS nueva no lo pisa.
    public string LosType { get; set; } = "";
    public AssetKind Kind { get; set; }
    public string LayoutName { get; set; } = "";
    public List<AssetVariable> Variables { get; set; } = [];
    // Operación asignada (su Id); vacío si se llama sin operación.
    public string? Operation { get; set; }
    // FB o UDT distintos de los del tipo, leídos de un FB.
    public string? FbOverride { get; set; }
    public string? UdtOverride { get; set; }
    // Creado a mano: la LoS no lo da por desaparecido.
    public bool Manual { get; set; }
    // Estaba en una LoS anterior y no en la última importada.
    public bool Missing { get; set; }
}

public sealed class AssetVariable
{
    public string Name { get; set; } = "";
    public string DataType { get; set; } = "";
    // OUT: del PLC al IPC. IN: del IPC al PLC.
    public string Direction { get; set; } = "OUT";
    // Dirección tal como la escribe la LoS: Bool_061_o(000)_00.
    public string Address { get; set; } = "";
    public string Description { get; set; } = "";

    [JsonIgnore]
    public bool IsOutput => Direction.Equals("OUT", StringComparison.OrdinalIgnoreCase);
}

public sealed class Location
{
    public string Name { get; set; } = "";
    public int Number { get; set; }
}

public sealed class Operation
{
    // Número de la operación (110) o el texto que se le quiera dar (DMC).
    public string Id { get; set; } = "";
    public List<OperationLocation> Locations { get; set; } = [];

    [JsonIgnore]
    public string VariableName => "Operation_" + Id;
}

public sealed class OperationLocation
{
    public string Location { get; set; } = "";
    // Señal de location virtual; vacía sale alwaysFalse.
    public string VirtualLoc { get; set; } = "";
    public List<ReferenceSignals> References { get; set; } = [];

    // Cada referencia es un bit: la 1 vale 1, la 2 vale 2, la 3 vale 4...
    [JsonIgnore]
    public long Mask => References.Aggregate(0L, (m, r) => m | (1L << (r.Reference - 1)));
}

public sealed class ReferenceSignals
{
    public const int MaxReferences = 8;

    public int Reference { get; set; }
    // Señales que se encadenan con Y; vacía sale alwaysFalse.
    public List<string> PartPresent { get; set; } = [];
    public List<string> ClampsOpen { get; set; } = [];
}

public sealed class Catalog
{
    public List<AssetType> Types { get; set; } = [];

    public AssetType? Find(string name) => Types.FirstOrDefault(t => t.Name == name);
}

public sealed class AssetType
{
    // Nombre en la LoS (Template): OP_MOVE_A.
    public string Name { get; set; } = "";
    // Equivalencia en el PLC: FB al que se llama y UDT de la variable del asset.
    public string Fb { get; set; } = "";
    public string Udt { get; set; } = "";
    // Pin del FB donde se conecta la variable del asset (OP_MOVE_CNV, AS_Robot...).
    public string UdtPin { get; set; } = "";
    public string UdtPinSection { get; set; } = "Output";
    // Pin de la operación; vacío si el FB no la recibe.
    public string OperationPin { get; set; } = "Operation";
    // Sin FB ni UDT (CE_PLC): no hay llamada ni variable del asset; cada variable se escribe desde su origen.
    public bool NoFb { get; set; }
    public List<TypeVariable> Variables { get; set; } = [];
    // Interfaz completa del FB, aprendida al leer un FB. Vacía si no se conoce.
    public List<FbParameter> Parameters { get; set; } = [];

    [JsonIgnore]
    public bool HasEquivalence => NoFb || (Fb.Length > 0 && Udt.Length > 0 && UdtPin.Length > 0);
}

public sealed class TypeVariable
{
    public string Name { get; set; } = "";
    public string DataType { get; set; } = "";
    // Solo en tipos sin FB: lo que se escribe en el device (#LifeBit, "DB".a.b). Vacío: no se escribe.
    public string Source { get; set; } = "";
}

public sealed record FbParameter(string Name, string Section, string Type);
