using System.Collections.ObjectModel;
using System.Globalization;
using System.Reflection;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Iris.Core;

namespace Iris.App.ViewModels;

public abstract class ViewModelBase : ObservableObject
{
    private bool _isBusy;
    private string? _errorMessage;

    public bool IsBusy { get => _isBusy; protected set => SetProperty(ref _isBusy, value); }
    public string? ErrorMessage
    {
        get => _errorMessage;
        protected set
        {
            if (SetProperty(ref _errorMessage, value))
                OnPropertyChanged(nameof(HasError));
        }
    }
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    protected async Task RunAsync(Func<Task> operation)
    {
        if (IsBusy)
            return;
        IsBusy = true;
        ErrorMessage = null;
        try { await operation(); }
        catch (OperationCanceledException) { ErrorMessage = "The operation was cancelled."; }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or ArgumentException
            or HttpRequestException or InvalidDataException or PlatformNotSupportedException
            or UnauthorizedAccessException or TimeoutException or FormatException or NotSupportedException)
        {
            ErrorMessage = exception.Message;
        }
        catch (Exception)
        {
            ErrorMessage = "An unexpected error occurred. See Logs & Diagnostics for details.";
        }
        finally { IsBusy = false; }
    }
}

public sealed class HomeViewModel : ViewModelBase
{
    private readonly IPlatformToolsManager _tools;
    private readonly IAdbService _adb;
    private readonly IFastbootService _fastboot;
    private readonly IDriverManager _drivers;
    private readonly IDeviceSelectionService _selection;
    private string _toolsStatus = "Checking...";
    private string _adbVersion = "Not detected";
    private string _fastbootVersion = "Not detected";
    private string _driverStatus = "Checking...";
    private DeviceInfo? _selectedDevice;
    private string _lastOutput = string.Empty;

    public HomeViewModel(IPlatformToolsManager tools, IAdbService adb, IFastbootService fastboot,
        IDriverManager drivers, IDeviceSelectionService selection)
    {
        _tools = tools;
        _adb = adb;
        _fastboot = fastboot;
        _drivers = drivers;
        _selection = selection;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        AdbDevicesCommand = new AsyncRelayCommand(() => RunCommandAsync(_adb, ["devices", "-l"]));
        FastbootDevicesCommand = new AsyncRelayCommand(() => RunCommandAsync(_fastboot, ["devices"]));
    }

    public string Version => GetType().Assembly.GetName().Version?.ToString(3) ?? "1.0.0";
    public ObservableCollection<DeviceInfo> Devices { get; } = [];
    public IAsyncRelayCommand RefreshCommand { get; }
    public IAsyncRelayCommand AdbDevicesCommand { get; }
    public IAsyncRelayCommand FastbootDevicesCommand { get; }
    public string ToolsStatus { get => _toolsStatus; private set => SetProperty(ref _toolsStatus, value); }
    public string AdbVersion { get => _adbVersion; private set => SetProperty(ref _adbVersion, value); }
    public string FastbootVersion { get => _fastbootVersion; private set => SetProperty(ref _fastbootVersion, value); }
    public string DriverStatus { get => _driverStatus; private set => SetProperty(ref _driverStatus, value); }
    public string LastOutput { get => _lastOutput; private set => SetProperty(ref _lastOutput, value); }
    public DeviceInfo? SelectedDevice
    {
        get => _selectedDevice;
        set
        {
            if (SetProperty(ref _selectedDevice, value))
                _selection.SelectedDevice = value;
        }
    }

    public Task RefreshAsync() => RunAsync(async () =>
    {
        var toolTask = _tools.DetectAsync();
        var adbTask = GetDevicesSafelyAsync(_adb.GetDevicesAsync);
        var fastbootTask = GetDevicesSafelyAsync(_fastboot.GetDevicesAsync);
        var driverTask = _drivers.DetectAsync();
        await Task.WhenAll(toolTask, adbTask, fastbootTask, driverTask);
        var tools = await toolTask;
        ToolsStatus = tools.AdbAvailable && tools.FastbootAvailable ? "Ready" : "Install platform-tools";
        AdbVersion = tools.AdbVersion ?? "Not detected";
        FastbootVersion = tools.FastbootVersion ?? "Not detected";
        var detectedDrivers = await driverTask;
        DriverStatus = $"{detectedDrivers.Count(driver => driver.IsInstalled)} of {detectedDrivers.Count} detected";
        Devices.Clear();
        foreach (var device in (await adbTask).Concat(await fastbootTask))
            Devices.Add(device);
        var previous = _selection.SelectedDevice;
        SelectedDevice = previous is null ? null : Devices.FirstOrDefault(device =>
            device.Serial == previous.Serial && device.Transport == previous.Transport);
    });

    public Task RebootAsync(string? mode) => RunAsync(async () =>
    {
        var target = SelectedDevice ?? throw new InvalidOperationException("Select a device first.");
        LastOutput = FormatResult(await RebootTargetCoreAsync(target, mode));
    });

    public Task RebootTargetAsync(DeviceInfo target, string? mode) => RunAsync(async () =>
    {
        ArgumentNullException.ThrowIfNull(target);
        LastOutput = FormatResult(await RebootTargetCoreAsync(target, mode));
    });

    private async Task<ProcessResult> RebootTargetCoreAsync(DeviceInfo target, string? mode)
    {
        if (target.Transport == DeviceTransport.Adb)
            return await _adb.RebootAsync(target.Serial, mode);
        return await _fastboot.RebootAsync(target.Serial, mode == "bootloader");
    }

    private Task RunCommandAsync(IAdbService service, IReadOnlyList<string> arguments) => RunAsync(async () =>
        LastOutput = FormatResult(await service.ExecuteAsync(arguments)));

    private Task RunCommandAsync(IFastbootService service, IReadOnlyList<string> arguments) => RunAsync(async () =>
        LastOutput = FormatResult(await service.ExecuteAsync(arguments)));

    private static async Task<IReadOnlyList<DeviceInfo>> GetDevicesSafelyAsync(
        Func<CancellationToken, Task<IReadOnlyList<DeviceInfo>>> operation)
    {
        try { return await operation(default); }
        catch (IOException) { return []; }
        catch (InvalidOperationException) { return []; }
    }

    internal static string FormatResult(ProcessResult result) => string.Join(Environment.NewLine,
        new[] { result.StandardOutput.TrimEnd(), result.StandardError.TrimEnd(), $"Exit code: {result.ExitCode} | {result.Duration.TotalSeconds:F2}s" }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
}

public sealed class DevicesViewModel : ViewModelBase
{
    private readonly IAdbService _adb;
    private readonly IFastbootService _fastboot;
    private readonly IDeviceSelectionService _selection;
    private DeviceInfo? _selectedDevice;

    public DevicesViewModel(IAdbService adb, IFastbootService fastboot, IDeviceSelectionService selection)
    {
        _adb = adb;
        _fastboot = fastboot;
        _selection = selection;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
    }

    public ObservableCollection<DeviceInfo> Devices { get; } = [];
    public IAsyncRelayCommand RefreshCommand { get; }
    public DeviceInfo? SelectedDevice
    {
        get => _selectedDevice;
        set
        {
            if (SetProperty(ref _selectedDevice, value))
                _selection.SelectedDevice = value;
        }
    }

    public Task RefreshAsync() => RunAsync(async () =>
    {
        IReadOnlyList<DeviceInfo> adbDevices = [];
        IReadOnlyList<DeviceInfo> fastbootDevices = [];
        try { adbDevices = await _adb.GetDevicesAsync(); } catch (IOException) { }
        try { fastbootDevices = await _fastboot.GetDevicesAsync(); } catch (IOException) { }
        Devices.Clear();
        foreach (var device in adbDevices.Concat(fastbootDevices))
            Devices.Add(device);
        var previous = _selection.SelectedDevice;
        SelectedDevice = previous is null ? null : Devices.FirstOrDefault(device =>
            device.Serial == previous.Serial && device.Transport == previous.Transport);
    });
}

public sealed class TerminalViewModel : ViewModelBase, IDisposable
{
    private readonly ICommandParser _parser;
    private readonly IAdbService _adb;
    private readonly IFastbootService _fastboot;
    private readonly IDeviceSelectionService _selection;
    private readonly ISettingsService _settings;
    private CancellationTokenSource? _cancellation;
    private string _commandText = "adb devices -l";
    private string _output = string.Empty;

    public TerminalViewModel(ICommandParser parser, IAdbService adb, IFastbootService fastboot,
        IDeviceSelectionService selection, ISettingsService settings)
    {
        _parser = parser;
        _adb = adb;
        _fastboot = fastboot;
        _selection = selection;
        _settings = settings;
        CancelCommand = new RelayCommand(() =>
        {
            try { _cancellation?.Cancel(); }
            catch (ObjectDisposedException) { }
        });
        ClearCommand = new RelayCommand(() => Output = string.Empty);
    }

    public ObservableCollection<string> History { get; } = [];
    public IRelayCommand CancelCommand { get; }
    public IRelayCommand ClearCommand { get; }
    public string CommandText { get => _commandText; set => SetProperty(ref _commandText, value); }
    public string Output { get => _output; private set => SetProperty(ref _output, value); }

    public CommandInvocation Parse() => _parser.Parse(CommandText);

    public Task ExecuteAsync(CommandInvocation invocation) => RunAsync(async () =>
    {
        using var cancellation = new CancellationTokenSource();
        _cancellation = cancellation;
        try
        {
        var selected = _selection.SelectedDevice;
        var firstArgument = invocation.Arguments.Count > 0 ? invocation.Arguments[0] : null;
        var isDiscovery = firstArgument?.Equals("devices", StringComparison.OrdinalIgnoreCase) == true ||
                          firstArgument?.Equals("version", StringComparison.OrdinalIgnoreCase) == true ||
                          firstArgument?.Equals("--version", StringComparison.OrdinalIgnoreCase) == true;
        string? serial = null;
        if (!isDiscovery)
        {
            if (selected is null)
                throw new InvalidOperationException("Select a target device on Home or Devices first.");
            var expectedTransport = invocation.Executable == "adb" ? DeviceTransport.Adb : DeviceTransport.Fastboot;
            if (selected.Transport != expectedTransport)
                throw new InvalidOperationException($"The selected device is connected through {selected.Transport}, not {expectedTransport}.");
            serial = selected.Serial;
        }

        var lines = new StringBuilder();
        var progress = new Progress<ProcessOutputLine>(line =>
        {
            lines.AppendLine(CultureInfo.CurrentCulture, $"[{line.Timestamp:HH:mm:ss}] {(line.IsError ? "ERR" : "OUT")} {line.Text}");
            Output = lines.ToString();
        });
        var result = invocation.Executable == "adb"
            ? await _adb.ExecuteAsync(invocation.Arguments, serial, progress, cancellation.Token)
            : await _fastboot.ExecuteAsync(invocation.Arguments, serial, progress, cancellation.Token);
        lines.AppendLine(CultureInfo.CurrentCulture, $"Exit code: {result.ExitCode} | {result.Duration.TotalSeconds:F2}s | Device: {serial ?? "none"}");
        Output = lines.ToString();
        History.Insert(0, CommandText);
        while (History.Count > _settings.Current.CommandHistoryRetention)
            History.RemoveAt(History.Count - 1);
        }
        finally
        {
            _cancellation = null;
        }
    });

    public void Dispose()
    {
        try { _cancellation?.Cancel(); }
        catch (ObjectDisposedException) { }
        _cancellation = null;
    }
}

public sealed class PlatformToolsViewModel : ViewModelBase
{
    private readonly IPlatformToolsManager _manager;
    private readonly ISettingsService _settings;
    private string _status = "Not checked";
    private double _progress;
    private string _expectedSha256 = string.Empty;
    private CancellationTokenSource? _installCts;

    public PlatformToolsViewModel(IPlatformToolsManager manager, ISettingsService settings)
    {
        _manager = manager;
        _settings = settings;
        DetectCommand = new AsyncRelayCommand(DetectAsync);
        InstallCommand = new AsyncRelayCommand(InstallAsync);
        CancelInstallCommand = new RelayCommand(() =>
        {
            try { _installCts?.Cancel(); }
            catch (ObjectDisposedException) { }
        });
    }

    public IAsyncRelayCommand DetectCommand { get; }
    public IAsyncRelayCommand InstallCommand { get; }
    public IRelayCommand CancelInstallCommand { get; }
    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public double Progress { get => _progress; private set => SetProperty(ref _progress, value); }
    public string ExpectedSha256
    {
        get => _expectedSha256;
        set => SetProperty(ref _expectedSha256, value?.Trim() ?? string.Empty);
    }
    public bool LicenseAccepted
    {
        get => _settings.Current.PlatformToolsLicenseAccepted;
        set { _settings.Current.PlatformToolsLicenseAccepted = value; OnPropertyChanged(); }
    }

    private Task DetectAsync() => RunAsync(async () =>
    {
        var status = await _manager.DetectAsync();
        Status = $"ADB: {status.AdbVersion ?? "not found"}{Environment.NewLine}Fastboot: {status.FastbootVersion ?? "not found"}";
    });

    private async Task InstallAsync()
    {
        await RunAsync(async () =>
        {
            await _settings.SaveAsync();
            var progress = new Progress<double>(value => Progress = value * 100);
            using var cts = new CancellationTokenSource();
            _installCts = cts;
            try
            {
                var sha = string.IsNullOrWhiteSpace(ExpectedSha256) ? null : ExpectedSha256.ToLowerInvariant();
                if (sha is not null && (sha.Length != 64 || !sha.All(Uri.IsHexDigit)))
                    throw new FormatException("Expected SHA-256 must contain 64 hexadecimal characters.");
                await _manager.InstallAsync(new Uri("https://dl.google.com/android/repository/platform-tools-latest-windows.zip"),
                    expectedSha256: sha, replaceExisting: true, progress, cancellationToken: cts.Token);
            }
            finally
            {
                _installCts = null;
            }
        });
        await DetectAsync();
    }
}

public sealed class DriversViewModel : ViewModelBase
{
    private readonly IDriverManager _manager;

    public DriversViewModel(IDriverManager manager)
    {
        _manager = manager;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
    }

    public ObservableCollection<DriverInfo> Drivers { get; } = [];
    public IAsyncRelayCommand RefreshCommand { get; }

    public Task RefreshAsync() => RunAsync(async () =>
    {
        Drivers.Clear();
        foreach (var driver in await _manager.DetectAsync())
            Drivers.Add(driver);
    });
}

public sealed class SamsungViewModel(ISamsungService samsung) : ViewModelBase
{
    private string _target = string.Empty;
    private string _blPath = string.Empty;
    private string _apPath = string.Empty;
    private string _cpPath = string.Empty;
    private string _cscPath = string.Empty;
    private string _userDataPath = string.Empty;
    private string _pitPath = string.Empty;
    private bool _rebootAfterFlash = true;
    private string _output = "Refresh targets, then enter or pick one exact target.";
    private BrokkrDeviceInfo? _selectedTarget;

    public ObservableCollection<BrokkrDeviceInfo> Targets { get; } = [];
    public string Target { get => _target; set => SetProperty(ref _target, value); }
    public string BlPath { get => _blPath; set => SetProperty(ref _blPath, value); }
    public string ApPath { get => _apPath; set => SetProperty(ref _apPath, value); }
    public string CpPath { get => _cpPath; set => SetProperty(ref _cpPath, value); }
    public string CscPath { get => _cscPath; set => SetProperty(ref _cscPath, value); }
    public string UserDataPath { get => _userDataPath; set => SetProperty(ref _userDataPath, value); }
    public string PitPath { get => _pitPath; set => SetProperty(ref _pitPath, value); }
    public bool RebootAfterFlash { get => _rebootAfterFlash; set => SetProperty(ref _rebootAfterFlash, value); }
    public string Output { get => _output; private set => SetProperty(ref _output, value); }
    public BrokkrDeviceInfo? SelectedTarget
    {
        get => _selectedTarget;
        set
        {
            if (SetProperty(ref _selectedTarget, value) && value is not null)
                Target = value.SysName;
        }
    }

    public Task RefreshTargetsAsync() => RunAsync(async () =>
    {
        var devices = await samsung.GetOdinDevicesAsync();
        Targets.Clear();
        foreach (var device in devices)
            Targets.Add(device);
        var previous = SelectedTarget?.SysName;
        SelectedTarget = previous is null ? null : Targets.FirstOrDefault(device => device.SysName == previous);
        if (SelectedTarget is not null)
            Target = SelectedTarget.SysName;
        Output = Targets.Count == 0
            ? "No Samsung devices reported by brokkR --list. Put the phone in Download (Odin) mode and install the Samsung USB driver."
            : $"brokkR --list reported {Targets.Count} device(s). Pick one exact target.";
    });

    public Task ListAsync() => RunAsync(async () => Output = HomeViewModel.FormatResult(await samsung.ListDevicesAsync()));

    public Task RebootDownloadAsync() => RunAsync(async () =>
        Output = HomeViewModel.FormatResult(await samsung.RebootDownloadAsync()));

    public Task FlashAsync() => RunAsync(async () =>
    {
        var request = new SamsungFlashRequest(Target, NullIfEmpty(BlPath), NullIfEmpty(ApPath),
            NullIfEmpty(CpPath), NullIfEmpty(CscPath), NullIfEmpty(UserDataPath), NullIfEmpty(PitPath),
            RebootAfterFlash);
        Output = HomeViewModel.FormatResult(await samsung.FlashAsync(request));
    });

    private static string? NullIfEmpty(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}

public sealed class DiagnosticsViewModel(IDiagnosticsService diagnostics, ILogService logs) : ViewModelBase
{
    private string _diagnosticsText = "Select Refresh to collect diagnostics.";
    public string DiagnosticsText { get => _diagnosticsText; private set => SetProperty(ref _diagnosticsText, value); }
    public string LogPath => logs.LogPath;
    public Task RefreshAsync() => RunAsync(async () => DiagnosticsText = (await diagnostics.CreateAsync()).ToString());
}

public sealed class SettingsViewModel(ISettingsService settings) : ViewModelBase
{
    public IReadOnlyList<AppTheme> Themes { get; } = Enum.GetValues<AppTheme>();
    public AppTheme Theme { get => settings.Current.Theme; set { settings.Current.Theme = value; OnPropertyChanged(); } }
    public string ToolDirectory { get => settings.Current.ToolDirectory; set { settings.Current.ToolDirectory = value; OnPropertyChanged(); } }
    public string DownloadDirectory { get => settings.Current.DownloadDirectory; set { settings.Current.DownloadDirectory = value; OnPropertyChanged(); } }
    public string BrokkrPath { get => settings.Current.BrokkrPath ?? string.Empty; set { settings.Current.BrokkrPath = value; OnPropertyChanged(); } }
    public bool AutomaticUpdates { get => settings.Current.CheckForUpdatesAutomatically; set { settings.Current.CheckForUpdatesAutomatically = value; OnPropertyChanged(); } }
    public Task SaveAsync() => RunAsync(() => settings.SaveAsync());
}

public sealed class AboutViewModel(IUpdateService updates) : ViewModelBase
{
    private UpdateCheckResult? _availableUpdate;
    private string _updateStatus = "Updates have not been checked.";
    private double _progress;
    public string Version => GetType().Assembly.GetName().Version?.ToString(3) ?? "1.0.0";
    public string UpdateStatus { get => _updateStatus; private set => SetProperty(ref _updateStatus, value); }
    public double Progress { get => _progress; private set => SetProperty(ref _progress, value); }
    public bool CanDownload => _availableUpdate?.IsUpdateAvailable == true;
    public Task CheckAsync() => RunAsync(async () =>
    {
        var result = await updates.CheckAsync(System.Version.TryParse(Version, out var current) ? current : new System.Version(1, 0, 0));
        _availableUpdate = result;
        OnPropertyChanged(nameof(CanDownload));
        UpdateStatus = result.IsUpdateAvailable
            ? $"Iris {result.AvailableVersion} is available. Review the release notes before downloading."
            : "Iris is up to date.";
    });

    public async Task<string?> PrepareAsync()
    {
        string? path = null;
        await RunAsync(async () =>
        {
            if (_availableUpdate is null || !_availableUpdate.IsUpdateAvailable)
                throw new InvalidOperationException("Check for updates before downloading.");
            path = await updates.DownloadAsync(_availableUpdate, new Progress<double>(value => Progress = value * 100));
            UpdateStatus = "The update passed integrity checks and is ready to install.";
        });
        return path;
    }
}
