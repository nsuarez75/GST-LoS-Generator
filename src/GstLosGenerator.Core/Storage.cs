// Guardado en disco: proyecto (.losproj), catálogo de tipos (.loscat) y ajustes de la app. Todo en JSON.
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GstLosGenerator.Core;

public static class Storage
{
    public const string ProjectExtension = ".losproj";
    public const string CatalogExtension = ".loscat";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        // Señales como ++1000+FIX01-PP01 se guardan legibles, sin escapar el +.
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string ToJson<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static T FromJson<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, Options) ?? throw new InvalidDataException(L.T("El archivo está vacío."));

    // Copia profunda pasando por JSON; la usa deshacer/rehacer.
    public static T Clone<T>(T value) => FromJson<T>(ToJson(value));

    public static void SaveProject(Project project, string path) => WriteAtomic(path, ToJson(project));

    public static Project LoadProject(string path) => Read<Project>(path);

    public static void SaveCatalog(Catalog catalog, string path) => WriteAtomic(path, ToJson(catalog));

    public static Catalog LoadCatalog(string path) => File.Exists(path) ? Read<Catalog>(path) : new Catalog();

    public static AppSettings LoadSettings(string path)
    {
        try
        {
            return File.Exists(path) ? Read<AppSettings>(path) : new AppSettings();
        }
        catch (Exception e) when (e is IOException or JsonException or InvalidDataException or UnauthorizedAccessException)
        {
            return new AppSettings();
        }
    }

    public static void SaveSettings(AppSettings settings, string path) => WriteAtomic(path, ToJson(settings));

    // Carpeta de datos de la app: %APPDATA%\GST-LoS-Generator.
    public static string AppDataFolder() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GST-LoS-Generator");

    private static T Read<T>(string path)
    {
        try
        {
            return FromJson<T>(File.ReadAllText(path));
        }
        catch (JsonException e)
        {
            throw new InvalidDataException(L.T("{0} no tiene un formato válido: {1}", Path.GetFileName(path), e.Message), e);
        }
    }

    // Se escribe en un temporal y se renombra, para no dejar un archivo a medias si algo falla.
    private static void WriteAtomic(string path, string content)
    {
        var folder = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(folder))
        {
            Directory.CreateDirectory(folder);
        }
        var temp = path + ".tmp";
        File.WriteAllText(temp, content);
        File.Move(temp, path, overwrite: true);
    }
}

public sealed class AppSettings
{
    public const string ThemeSystem = "system";
    public const string ThemeLight = "light";
    public const string ThemeDark = "dark";

    public string Theme { get; set; } = ThemeSystem;
    // Vacío: el idioma de Windows.
    public string? Language { get; set; }
    public string? LastProject { get; set; }
}
