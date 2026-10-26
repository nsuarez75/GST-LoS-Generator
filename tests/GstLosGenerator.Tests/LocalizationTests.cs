using System.Text.RegularExpressions;
using GstLosGenerator.Core;

namespace GstLosGenerator.Tests;

public partial class LocalizationTests
{
    // Todo texto de la interfaz, en el código (L.T("...")) o en las vistas ({l:T '...'}), tiene traducción al inglés.
    [Fact]
    public void Todos_los_textos_tienen_traduccion_al_ingles()
    {
        var src = Path.Combine(RepoRoot(), "src");
        var keys = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories).Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")))
        {
            foreach (Match m in CodeText().Matches(File.ReadAllText(file)))
            {
                keys.Add(Regex.Unescape(m.Groups[1].Value));
            }
        }
        foreach (var file in Directory.EnumerateFiles(src, "*.axaml", SearchOption.AllDirectories))
        {
            foreach (Match m in ViewText().Matches(File.ReadAllText(file)))
            {
                keys.Add(System.Net.WebUtility.HtmlDecode(m.Groups[1].Value));
            }
        }

        var missing = keys.Where(k => !L.EnglishTexts.ContainsKey(k)).ToList();

        Assert.True(missing.Count == 0, "Sin traducción:\n" + string.Join("\n", missing));
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "GstLosGenerator.slnx")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? throw new InvalidOperationException("No se encuentra la raíz del repo.");
    }

    [GeneratedRegex(@"L\.T\(""((?:[^""\\]|\\.)*)""")]
    private static partial Regex CodeText();

    [GeneratedRegex(@"\{l:T '((?:[^'])*)'\}")]
    private static partial Regex ViewText();
}
