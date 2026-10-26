// Idioma de la interfaz. Los textos se escriben en castellano en el código y L.T los traduce:
// el texto en castellano es la clave, así que un texto sin traducción se muestra tal cual.
// El código generado (FB y UDT) no pasa por aquí: no cambia con el idioma.
using System.Globalization;

namespace GstLosGenerator.Core;

public static partial class L
{
    public const string Spanish = "es";
    public const string English = "en";

    public static string Language { get; set; } = Spanish;

    public static string Normalize(string? language) => language == English ? English : Spanish;

    // Idioma de Windows: inglés salvo que sea castellano.
    public static string SystemLanguage() =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == Spanish ? Spanish : English;

    public static IReadOnlyDictionary<string, string> EnglishTexts => En;

    public static string T(string text) => Language == English && En.TryGetValue(text, out var translated) ? translated : text;

    // Textos con huecos {0}, {1}...: se traduce el patrón y después se rellenan.
    public static string T(string format, params object?[] args) => string.Format(CultureInfo.InvariantCulture, T(format), args);
}
