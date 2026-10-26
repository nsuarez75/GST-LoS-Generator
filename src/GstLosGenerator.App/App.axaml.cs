using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using GstLosGenerator.App.ViewModels;
using GstLosGenerator.App.Views;
using GstLosGenerator.Core;

namespace GstLosGenerator.App;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public static string SettingsPath => Path.Combine(Storage.AppDataFolder(), "ajustes.json");
    public static string CatalogPath => Path.Combine(Storage.AppDataFolder(), "catalogo" + Storage.CatalogExtension);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var settings = Storage.LoadSettings(SettingsPath);
            L.Language = settings.Language is { } saved ? L.Normalize(saved) : L.SystemLanguage();
            ApplyTheme(settings.Theme);
            var window = new MainWindow();
            var session = new Session(CatalogPath);
            var main = new MainViewModel(session, window, settings, SaveSettings);
            window.Attach(main);
            desktop.MainWindow = window;
            var language = L.Language;
            main.AppearanceChanged += () =>
            {
                ApplyTheme(settings.Theme);
                if (L.Language == language)
                {
                    return;
                }
                // Los textos se traducen al cargar las vistas: con otro idioma se monta una ventana nueva.
                language = L.Language;
                var old = (MainWindow)desktop.MainWindow!;
                var fresh = new MainWindow { Width = old.Width, Height = old.Height, Position = old.Position, WindowState = old.WindowState };
                fresh.Attach(main);
                main.Rebuild();
                desktop.MainWindow = fresh;
                fresh.Show();
                old.CloseSilently();
            };
            window.Opened += async (_, _) =>
            {
                using var http = new HttpClient();
                await main.CheckForUpdates(cancel => UpdateCheck.LatestRelease(http, cancel));
            };
            if (settings.LastProject is { } last && File.Exists(last))
            {
                _ = main.OpenPath(last);
            }
            else
            {
                session.New();
            }
        }
        base.OnFrameworkInitializationCompleted();
    }

    public static void ApplyTheme(string theme)
    {
        if (Current is not null)
        {
            Current.RequestedThemeVariant = theme switch
            {
                AppSettings.ThemeLight => ThemeVariant.Light,
                AppSettings.ThemeDark => ThemeVariant.Dark,
                _ => ThemeVariant.Default,
            };
        }
    }

    private static void SaveSettings(AppSettings settings)
    {
        try
        {
            Storage.SaveSettings(settings, SettingsPath);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Los ajustes son una comodidad: si no se pueden guardar, la app sigue igual.
        }
    }
}
