// Comprobación de versión nueva: pregunta a GitHub por la última release del repo y la compara con la versión de la app.
// Si no hay red, el repo no es accesible o la respuesta no se entiende, no dice nada.
using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;

namespace GstLosGenerator.Core;

public sealed record ReleaseInfo(string Tag, Version Version, string Url);

public static class UpdateCheck
{
    public const string Repository = "nsuarez75/GST-LoS-Generator";
    public const string RepositoryUrl = "https://github.com/" + Repository;

    public static async Task<ReleaseInfo?> LatestRelease(HttpClient http, CancellationToken cancel = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{Repository}/releases/latest");
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue("GST-LoS-Generator", "1"));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            using var response = await http.SendAsync(request, cancel);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }
            return Parse(await response.Content.ReadAsStringAsync(cancel));
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            return null;
        }
    }

    // Respuesta de /releases/latest: tag_name (v1.2.0) y html_url.
    public static ReleaseInfo? Parse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var tag = root.TryGetProperty("tag_name", out var t) ? t.GetString() : null;
            var url = root.TryGetProperty("html_url", out var u) ? u.GetString() : null;
            return tag is not null && ParseVersion(tag) is { } version ? new ReleaseInfo(tag, version, url ?? RepositoryUrl + "/releases") : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // v1.2 o 1.2.3 -> versión con tres números.
    public static Version? ParseVersion(string text)
    {
        var clean = text.Trim().TrimStart('v', 'V');
        var dash = clean.IndexOfAny(['-', '+']);
        if (dash >= 0)
        {
            clean = clean[..dash];
        }
        var parts = clean.Split('.');
        if (parts.Length is < 1 or > 4 || !parts.All(p => int.TryParse(p, NumberStyles.None, CultureInfo.InvariantCulture, out _)))
        {
            return null;
        }
        var numbers = parts.Select(p => int.Parse(p, CultureInfo.InvariantCulture)).Concat([0, 0]).Take(3).ToArray();
        return new Version(numbers[0], numbers[1], numbers[2]);
    }

    public static bool IsNewer(ReleaseInfo release, Version current) =>
        release.Version > new Version(current.Major, current.Minor, Math.Max(current.Build, 0));
}
