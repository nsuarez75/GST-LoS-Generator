using Avalonia;
using Avalonia.Headless;
using GstLosGenerator.App.Tests;

[assembly: AvaloniaTestApplication(typeof(TestApp))]

namespace GstLosGenerator.App.Tests;

public static class TestApp
{
    // Sin pantalla pero dibujando de verdad con Skia, para poder sacar capturas.
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UseSkia()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
        .WithInterFont();
}
