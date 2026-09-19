namespace Iris.Core;

public interface IProcessRunner
{
    Task<ProcessResult> RunAsync(
        ProcessRequest request,
        IProgress<ProcessOutputLine>? progress = null,
        CancellationToken cancellationToken = default);
}

public interface ICommandParser
{
    CommandInvocation Parse(string input);
}

public interface IAdbService
{
    Task<string?> GetVersionAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DeviceInfo>> GetDevicesAsync(CancellationToken cancellationToken = default);
    Task<ProcessResult> ExecuteAsync(IReadOnlyList<string> arguments, string? serial = null,
        IProgress<ProcessOutputLine>? progress = null, CancellationToken cancellationToken = default);
    Task<ProcessResult> RebootAsync(string serial, string? mode = null, CancellationToken cancellationToken = default);
}

public interface IFastbootService
{
    Task<string?> GetVersionAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DeviceInfo>> GetDevicesAsync(CancellationToken cancellationToken = default);
    Task<ProcessResult> ExecuteAsync(IReadOnlyList<string> arguments, string? serial = null,
        IProgress<ProcessOutputLine>? progress = null, CancellationToken cancellationToken = default);
    Task<ProcessResult> RebootAsync(string serial, bool bootloader = false, CancellationToken cancellationToken = default);
}

public interface IDownloadService
{
    Task<DownloadResult> DownloadAsync(DownloadRequest request, IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);
    Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken = default);
}

public interface IPlatformToolsManager
{
    Task<ToolStatus> DetectAsync(CancellationToken cancellationToken = default);
    Task InstallAsync(Uri officialArchive, string? expectedSha256, bool replaceExisting,
        IProgress<double>? progress = null, CancellationToken cancellationToken = default);
    string? ResolveAdbPath();
    string? ResolveFastbootPath();
}

public interface IDriverManager
{
    Task<IReadOnlyList<DriverInfo>> DetectAsync(CancellationToken cancellationToken = default);
}

public interface ISamsungService
{
    Task<ProcessResult> ListDevicesAsync(IProgress<ProcessOutputLine>? progress = null,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BrokkrDeviceInfo>> GetOdinDevicesAsync(CancellationToken cancellationToken = default);
    Task<ProcessResult> RebootDownloadAsync(IProgress<ProcessOutputLine>? progress = null,
        CancellationToken cancellationToken = default);
    Task<ProcessResult> FlashAsync(SamsungFlashRequest request, IProgress<ProcessOutputLine>? progress = null,
        CancellationToken cancellationToken = default);
}

public interface IUpdateService
{
    Task<UpdateCheckResult> CheckAsync(Version currentVersion, CancellationToken cancellationToken = default);
    Task<string> DownloadAsync(UpdateCheckResult update, IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);
}

public interface ILogService
{
    string LogPath { get; }
    void Write(string level, string eventName, string message, Exception? exception = null);
    Task<string> ExportAsync(string destinationPath, CancellationToken cancellationToken = default);
}

public interface ISettingsService
{
    AppSettings Current { get; }
    Task LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(CancellationToken cancellationToken = default);
}

public interface IDiagnosticsService
{
    Task<DiagnosticReport> CreateAsync(CancellationToken cancellationToken = default);
    Task ExportAsync(DiagnosticReport report, string destinationPath,
        CancellationToken cancellationToken = default);
}

public interface IDeviceSelectionService
{
    DeviceInfo? SelectedDevice { get; set; }
    event EventHandler? SelectionChanged;
}
