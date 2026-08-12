using Avalonia;
using Avalonia.Fonts.Inter;

namespace OmniExplorer.Desktop;

internal static class Program
{
    internal static string? StartupRoot { get; private set; }

    internal static string? StartupTheme { get; private set; }

    [STAThread]
    public static void Main(string[] args)
    {
        StartupRoot = ReadStartupRoot(args);
        StartupTheme = ReadOption(args, "--theme");
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    private static string? ReadStartupRoot(string[] args) => ReadOption(args, "--root");

    private static string? ReadOption(string[] args, string option)
    {
        for (var index = 0; index < args.Length - 1; index++)
        {
            if (string.Equals(args[index], option, StringComparison.OrdinalIgnoreCase))
            {
                return args[index + 1];
            }
        }

        return null;
    }
}
