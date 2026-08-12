using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using OmniBrille.Desktop;

[assembly: AvaloniaTestApplication(typeof(OmniBrille.HeadlessTests.TestAppBuilder))]

namespace OmniBrille.HeadlessTests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder
        .Configure<App>()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = true });
}
