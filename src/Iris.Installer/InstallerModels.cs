using System.Runtime.InteropServices;

namespace Iris.Installer;

public enum InstallerStep { Welcome, SystemCheck, Options, Location, Components, Ready, Installing, Completed }

public sealed class InstallOptions
{
    public string InstallDirectory { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Iris");
    public bool InstallApplication { get; set; } = true;
    public bool OfferPlatformToolsOnFirstRun { get; set; } = true;
    public bool OfferAndroidDriver { get; set; }
    public bool OfferSamsungDriver { get; set; }
    public bool StartMenuShortcut { get; set; } = true;
    public bool DesktopShortcut { get; set; }
    public bool RemoveSettingsOnUninstall { get; set; }
    public bool RemoveDownloadedToolsOnUninstall { get; set; }
}

public sealed record SystemCheckResult(
    bool WindowsSupported,
    string Architecture,
    bool ArchitectureSupported,
    bool DiskSpaceSufficient,
    bool IsAdministrator,
    string? ExistingVersion)
{
    public bool CanInstall => WindowsSupported && ArchitectureSupported && DiskSpaceSufficient;
}

public sealed class PayloadManifest
{
    public required string Version { get; init; }
    public required string Architecture { get; init; }
    public required Dictionary<string, string> Files { get; init; }
}

public static class InstallerArchitecture
{
    public static string Current => RuntimeInformation.ProcessArchitecture switch
    {
        Architecture.X86 => "x86",
        Architecture.X64 => "x64",
        Architecture.Arm64 => "arm64",
        _ => "unsupported"
    };
}
