using System.Runtime.InteropServices;

namespace Iris.Core;

public enum DeviceTransport { Adb, Fastboot }
public enum DeviceState { Device, Unauthorized, Offline, Recovery, Bootloader, Fastboot, Unknown }
public enum AppTheme { System, Light, Dark }
public enum IrisArchitecture { X86, X64, Arm64 }

public sealed record DeviceInfo(
    string Serial,
    DeviceTransport Transport,
    DeviceState State,
    string? Product = null,
    string? Model = null,
    string? Connection = null)
{
    public string DisplayName => string.IsNullOrWhiteSpace(Model) ? Serial : $"{Model} ({Serial})";
}

public sealed record ProcessRequest(
    string FileName,
    IReadOnlyList<string> Arguments,
    TimeSpan? Timeout = null,
    string? WorkingDirectory = null);

public sealed record ProcessOutputLine(DateTimeOffset Timestamp, bool IsError, string Text);

public sealed record ProcessResult(
    int ExitCode,
    string StandardOutput,
    string StandardError,
    TimeSpan Duration,
    bool WasCancelled,
    bool TimedOut)
{
    public bool Succeeded => ExitCode == 0 && !WasCancelled && !TimedOut;
}

public sealed record ToolStatus(
    bool AdbAvailable,
    bool FastbootAvailable,
    string? AdbPath,
    string? FastbootPath,
    string? AdbVersion,
    string? FastbootVersion);

public sealed record DownloadRequest(
    Uri Source,
    string DestinationPath,
    string? ExpectedSha256 = null,
    IReadOnlySet<string>? AllowedExtensions = null,
    int MaxRetries = 2);

public sealed record DownloadResult(string Path, long Bytes, string Sha256);

public sealed record DriverInfo(
    string Name,
    string Source,
    Uri InformationUri,
    bool IsInstalled,
    string? Version,
    bool RequiresAdministrator,
    string InstallationNote);

public sealed record CommandInvocation(string Executable, IReadOnlyList<string> Arguments)
{
    public bool ChangesDeviceState
    {
        get
        {
            if (Arguments.Count == 0)
                return false;
            if (Executable.Equals("fastboot", StringComparison.OrdinalIgnoreCase))
                return Arguments[0] is not ("devices" or "--version" or "getvar");
            if (Arguments[0] is "devices" or "version" or "get-state" or "get-serialno")
                return false;
            return !(Arguments.Count == 2 && Arguments[0] == "shell" && Arguments[1] == "getprop");
        }
    }
}

public sealed record CommandRecord(
    DateTimeOffset Timestamp,
    string Command,
    string? DeviceSerial,
    ProcessResult Result);

public sealed class AppSettings
{
    public AppTheme Theme { get; set; } = AppTheme.System;
    public string ToolDirectory { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Iris", "tools");
    public string DownloadDirectory { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "Iris");
    public bool CheckForUpdatesAutomatically { get; set; } = true;
    public string UpdateChannel { get; set; } = "update";
    public int CommandHistoryRetention { get; set; } = 100;
    public string LoggingLevel { get; set; } = "Information";
    public string? BrokkrPath { get; set; }
    public bool PlatformToolsLicenseAccepted { get; set; }
}

public sealed class UpdateMetadata
{
    public required string Version { get; init; }
    public required DateOnly ReleaseDate { get; init; }
    public required string Channel { get; init; }
    public required string MinimumVersion { get; init; }
    public required string ReleaseNotes { get; init; }
    public required Dictionary<string, UpdatePackage> Architectures { get; init; }
}

public sealed class UpdatePackage
{
    public required Uri Url { get; init; }
    public required string Sha256 { get; init; }
}

public sealed record UpdateCheckResult(
    Version CurrentVersion,
    Version? AvailableVersion,
    UpdateMetadata? Metadata,
    UpdatePackage? Package)
{
    public bool IsUpdateAvailable => AvailableVersion is not null && Package is not null;
}

public sealed record BrokkrDeviceInfo(string SysName, bool IsOdinMode);

public sealed record SamsungFlashRequest(
    string Target,
    string? BlPath,
    string? ApPath,
    string? CpPath,
    string? CscPath,
    string? UserDataPath,
    string? PitPath,
    bool RebootAfterFlash);

public sealed record DiagnosticReport(DateTimeOffset CreatedAt, IReadOnlyDictionary<string, string> Values)
{
    public override string ToString() => string.Join(Environment.NewLine,
        Values.Select(pair => $"{pair.Key}: {pair.Value}"));
}

public static class ArchitectureInfo
{
    public static IrisArchitecture Current => RuntimeInformation.ProcessArchitecture switch
    {
        Architecture.X86 => IrisArchitecture.X86,
        Architecture.X64 => IrisArchitecture.X64,
        Architecture.Arm64 => IrisArchitecture.Arm64,
        _ => throw new PlatformNotSupportedException(
            $"Unsupported architecture: {RuntimeInformation.OSArchitecture}")
    };

    public static string MetadataKey(IrisArchitecture architecture) => architecture switch
    {
        IrisArchitecture.X86 => "x86",
        IrisArchitecture.X64 => "x64",
        IrisArchitecture.Arm64 => "arm64",
        _ => throw new ArgumentOutOfRangeException(nameof(architecture))
    };
}
