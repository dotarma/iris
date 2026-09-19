using Iris.App.ViewModels;
using Iris.Core;
using Iris.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;

namespace Iris.App;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        InitializeComponent();
        UnhandledException += (_, args) =>
        {
            Services.GetRequiredService<ILogService>().Write("Error", "app.unhandled", args.Message, args.Exception);
        };
        Services = ConfigureServices();
    }

    public static IServiceProvider Services { get; private set; } = null!;
    public static T Get<T>() where T : notnull => Services.GetRequiredService<T>();
    internal Window RootWindow => _window ?? throw new InvalidOperationException("The main window has not been created.");

    internal static void RefreshTheme()
    {
        try
        {
            if (Current is not App app || app._window?.Content is not FrameworkElement root)
                return;
            root.RequestedTheme = Services.GetRequiredService<ISettingsService>().Current.Theme switch
            {
                AppTheme.Light => ElementTheme.Light,
                AppTheme.Dark => ElementTheme.Dark,
                _ => ElementTheme.Default
            };
        }
        catch (InvalidOperationException) { }
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        var settings = Services.GetRequiredService<ISettingsService>();
        try
        {
            await settings.LoadAsync();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            Services.GetRequiredService<ILogService>().Write("Warning", "settings.load", "Settings could not be loaded; defaults are active.", exception);
        }

        Services.GetRequiredService<ILogService>().Write("Information", "app.start", "Iris started.");
        _window = new MainWindow();
        _window.Activate();

        if (settings.Current.CheckForUpdatesAutomatically)
            _ = CheckForUpdatesInBackgroundAsync();
    }

    private static async Task CheckForUpdatesInBackgroundAsync()
    {
        try
        {
            var updates = Services.GetRequiredService<IUpdateService>();
            var versionText = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
            var current = System.Version.TryParse(versionText, out var parsed) ? parsed : new Version(1, 0, 0);
            var result = await updates.CheckAsync(current).ConfigureAwait(false);
            Services.GetRequiredService<ILogService>().Write("Information", "update.startup-check",
                result.IsUpdateAvailable ? $"Update available: {result.AvailableVersion}." : "No update available at startup.");
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or HttpRequestException
            or InvalidDataException or PlatformNotSupportedException or UnauthorizedAccessException or TimeoutException)
        {
            Services.GetRequiredService<ILogService>().Write("Warning", "update.startup-check",
                "Automatic update check failed.", exception);
        }
        catch (Exception exception)
        {
            Services.GetRequiredService<ILogService>().Write("Warning", "update.startup-check",
                "Automatic update check failed unexpectedly.", exception);
        }
    }

    private static ServiceProvider ConfigureServices()
    {
        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
        var services = new ServiceCollection();
        services.AddSingleton(new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(30),
            DefaultRequestHeaders = { UserAgent = { new("Iris", version) } }
        });
        services.AddSingleton<ILogService, JsonLogService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IProcessRunner, ProcessRunner>();
        services.AddSingleton<ICommandParser, RestrictedCommandParser>();
        services.AddSingleton<IDownloadService, DownloadService>();
        services.AddSingleton<IPlatformToolsManager, PlatformToolsManager>();
        services.AddSingleton<IAdbService, AdbService>();
        services.AddSingleton<IFastbootService, FastbootService>();
        services.AddSingleton<IDriverManager, DriverManager>();
        services.AddSingleton<ISamsungService, SamsungService>();
        services.AddSingleton<IUpdateService, UpdateService>();
        services.AddSingleton<IDiagnosticsService, DiagnosticsService>();
        services.AddSingleton<IDeviceSelectionService, DeviceSelectionService>();
        services.AddTransient<HomeViewModel>();
        services.AddTransient<DevicesViewModel>();
        services.AddTransient<TerminalViewModel>();
        services.AddTransient<PlatformToolsViewModel>();
        services.AddTransient<DriversViewModel>();
        services.AddTransient<SamsungViewModel>();
        services.AddTransient<DiagnosticsViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<AboutViewModel>();
        return services.BuildServiceProvider();
    }
}
