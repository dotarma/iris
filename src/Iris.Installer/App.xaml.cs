using Microsoft.UI.Xaml;

namespace Iris.Installer;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        InitializeComponent();
        UnhandledException += (_, args) => WriteStartupLog($"UNHANDLED: {args.Message} {args.Exception}");
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            WriteStartupLog($"Installer started. BaseDirectory={AppContext.BaseDirectory} Args={string.Join(' ', Environment.GetCommandLineArgs().Skip(1))}");
            _window = new MainWindow();
            _window.Activate();
            WriteStartupLog("MainWindow activated.");
        }
        catch (Exception exception)
        {
            WriteStartupLog($"STARTUP FAILED: {exception}");
            throw;
        }
    }

    internal static void WriteStartupLog(string message)
    {
        try
        {
            var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Iris");
            Directory.CreateDirectory(directory);
            File.AppendAllText(Path.Combine(directory, "installer.log"),
                $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}");
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
