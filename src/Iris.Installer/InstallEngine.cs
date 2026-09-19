using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text.Json;
using Microsoft.Win32;

namespace Iris.Installer;

public sealed class InstallEngine
{
    private const string UninstallKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Iris";

    public static SystemCheckResult Check(InstallOptions options)
    {
        var os = Environment.OSVersion.Version;
        var windowsSupported = OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041);
        var architecture = InstallerArchitecture.Current;
        var architectureSupported = architecture is "x86" or "x64" or "arm64";
        var root = Path.GetPathRoot(Path.GetFullPath(options.InstallDirectory))!;
        var disk = new DriveInfo(root);
        var diskSufficient = disk.AvailableFreeSpace >= 750L * 1024 * 1024;
        var administrator = new WindowsPrincipal(WindowsIdentity.GetCurrent())
            .IsInRole(WindowsBuiltInRole.Administrator);
        return new SystemCheckResult(windowsSupported, architecture, architectureSupported,
            diskSufficient, administrator, ReadInstalledVersion());
    }

    public static async Task InstallAsync(InstallOptions options, IProgress<(double Value, string Status)> progress,
        CancellationToken cancellationToken = default)
    {
        var source = Path.Combine(AppContext.BaseDirectory, "payload", "Iris.App");
        var manifestPath = Path.Combine(source, "payload.manifest.json");
        if (!File.Exists(manifestPath))
            throw new InvalidDataException("The installer payload manifest is missing. Download a complete Iris installer package.");
        var manifest = JsonSerializer.Deserialize<PayloadManifest>(await File.ReadAllTextAsync(manifestPath, cancellationToken))
            ?? throw new InvalidDataException("The installer payload manifest is invalid.");
        ValidateManifest(manifest, source);
        var incoming = Version.Parse(manifest.Version);
        var installedText = ReadInstalledVersion();
        if (installedText is not null && Version.TryParse(installedText, out var installed) && incoming < installed)
            throw new InvalidOperationException($"Downgrade blocked: Iris {installed} is installed, but this package contains {incoming}.");

        var destination = Path.GetFullPath(options.InstallDirectory);
        ValidateInstallPath(destination);
        var check = Check(options);
        if (!check.CanInstall)
            throw new InvalidOperationException("System requirements are not satisfied.");
        if (destination.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), StringComparison.OrdinalIgnoreCase) && !check.IsAdministrator)
            throw new UnauthorizedAccessException("Administrator privileges are required for the selected Program Files location. Restart the installer as administrator or choose a user-writable location.");

        var appDestination = Path.Combine(destination, "App");
        var parent = Path.GetDirectoryName(destination)
            ?? throw new ArgumentException("The installation directory must have a parent folder.", nameof(options));
        var staging = Path.Combine(parent, $".Iris-{Guid.NewGuid():N}.staging");
        var backup = destination + $".{Guid.NewGuid():N}.backup";
        var backupCreated = false;
        try
        {
            progress.Report((0.05, "Preparing installation"));
            Directory.CreateDirectory(Path.Combine(staging, "App"));
            var copied = 0;
            foreach (var pair in manifest.Files.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var relative = ValidateRelativePath(pair.Key);
                var sourceFile = Path.GetFullPath(Path.Combine(source, relative));
                if (!sourceFile.StartsWith(Path.GetFullPath(source) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("The payload contains an unsafe path.");
                var destinationFile = Path.GetFullPath(Path.Combine(staging, "App", relative));
                if (!destinationFile.StartsWith(Path.GetFullPath(Path.Combine(staging, "App")) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("The payload contains an unsafe path.");
                Directory.CreateDirectory(Path.GetDirectoryName(destinationFile)!);
                await VerifyAndCopyAsync(sourceFile, destinationFile, pair.Value, cancellationToken).ConfigureAwait(false);
                copied++;
                progress.Report((0.1 + (0.6 * copied / manifest.Files.Count), $"Installing application ({copied}/{manifest.Files.Count})"));
            }

            CopyInstallerFiles(AppContext.BaseDirectory, Path.Combine(staging, "Installer"));

            await File.WriteAllTextAsync(Path.Combine(staging, "install-options.json"),
                JsonSerializer.Serialize(new
                {
                    offerPlatformTools = options.OfferPlatformToolsOnFirstRun,
                    offerAndroidDriver = options.OfferAndroidDriver,
                    offerSamsungDriver = options.OfferSamsungDriver
                }), cancellationToken).ConfigureAwait(false);

            if (Directory.Exists(destination))
            {
                Directory.Move(destination, backup);
                backupCreated = true;
            }

            try
            {
                Directory.Move(staging, destination);
            }
            catch
            {
                if (backupCreated)
                {
                    if (Directory.Exists(destination))
                        Directory.Move(destination, staging + ".failed");
                    Directory.Move(backup, destination);
                }

                throw;
            }

            try
            {
                progress.Report((0.78, "Creating shortcuts"));
                var executable = Path.Combine(appDestination, "Iris.App.exe");
                if (options.StartMenuShortcut)
                    Shortcut.Create(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", "Iris.lnk"), executable, appDestination);
                if (options.DesktopShortcut)
                    Shortcut.Create(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Iris.lnk"), executable, appDestination);

                progress.Report((0.9, "Registering uninstall information"));
                RegisterUninstall(destination, manifest.Version, check.IsAdministrator);
            }
            catch
            {
                if (backupCreated)
                {
                    if (Directory.Exists(destination))
                        Directory.Delete(destination, recursive: true);
                    Directory.Move(backup, destination);
                }

                throw;
            }

            if (backupCreated && Directory.Exists(backup))
                Directory.Delete(backup, recursive: true);
            progress.Report((1, "Iris installed successfully"));
        }
        catch
        {
            throw;
        }
        finally
        {
            if (Directory.Exists(staging))
                Directory.Delete(staging, recursive: true);
        }
    }

    public static async Task UninstallAsync(InstallOptions options, IProgress<(double Value, string Status)> progress)
    {
        var destination = Path.GetFullPath(ResolveUninstallRoot(options.InstallDirectory));
        ValidateUninstallRoot(destination);
        progress.Report((0.1, "Removing application files"));
        await Task.Run(() =>
        {
            DeleteOrThrow(Path.Combine(destination, "App"));
            TryDelete(Path.Combine(destination, "install-options.json"));
            TryDelete(Path.Combine(destination, "package.manifest.json"));
            TryDelete(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", "Iris.lnk"));
            TryDelete(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Iris.lnk"));
        }).ConfigureAwait(false);
        progress.Report((0.7, "Preserving user data"));
        var userData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Iris");
        if (options.RemoveSettingsOnUninstall)
            DeleteOrThrow(Path.Combine(userData, "settings.json"));
        if (options.RemoveDownloadedToolsOnUninstall)
            DeleteOrThrow(Path.Combine(userData, "tools"));
        DeleteUninstallRegistration();
        ScheduleDirectoryDeletion(Path.Combine(destination, "Installer"));
        _ = MoveFileEx(destination, null, MoveFileFlags.DelayUntilReboot);
        progress.Report((1, "Iris uninstalled"));
    }

    private static void ValidateManifest(PayloadManifest manifest, string source)
    {
        if (!Version.TryParse(manifest.Version, out var version) || version.Build < 0 || version.Revision >= 0)
            throw new InvalidDataException("The payload version must use MAJOR.MINOR.PATCH.");
        if (!manifest.Architecture.Equals(InstallerArchitecture.Current, StringComparison.OrdinalIgnoreCase))
            throw new PlatformNotSupportedException($"This {manifest.Architecture} package cannot run on {InstallerArchitecture.Current}.");
        if (manifest.Files.Count is 0 or > 10000 || !manifest.Files.ContainsKey("Iris.App.exe"))
            throw new InvalidDataException("The payload file list is invalid.");
        foreach (var pair in manifest.Files)
        {
            var relative = ValidateRelativePath(pair.Key);
            if (pair.Value.Length != 64 || !pair.Value.All(Uri.IsHexDigit) || !File.Exists(Path.Combine(source, relative)))
                throw new InvalidDataException($"Payload entry '{pair.Key}' is invalid.");
        }
    }

    private static string ValidateRelativePath(string path)
    {
        if (Path.IsPathFullyQualified(path) || path.Contains("..", StringComparison.Ordinal) ||
            path.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
            throw new InvalidDataException("The payload contains an unsafe path.");
        return path.Replace('/', Path.DirectorySeparatorChar);
    }

    private static async Task VerifyAndCopyAsync(string sourcePath, string destinationPath, string expected, CancellationToken cancellationToken)
    {
        if (expected.Length != 64 || !expected.All(Uri.IsHexDigit))
            throw new InvalidDataException($"Payload entry '{Path.GetFileName(sourcePath)}' is invalid.");
        await using var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read,
            81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var actual = Convert.ToHexString(await SHA256.HashDataAsync(source, cancellationToken).ConfigureAwait(false));
        if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Payload integrity verification failed for {Path.GetFileName(sourcePath)}.");
        source.Position = 0;
        await using var destination = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
            81920, FileOptions.Asynchronous | FileOptions.WriteThrough);
        await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
        await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void ValidateInstallPath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var root = Path.GetPathRoot(fullPath);
        if (string.IsNullOrWhiteSpace(root) || fullPath.Equals(root, StringComparison.OrdinalIgnoreCase) ||
            fullPath.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.Windows), StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Choose a dedicated installation directory outside the Windows directory.");

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        if (fullPath.Equals(programFiles, StringComparison.OrdinalIgnoreCase) ||
            fullPath.Equals(programFilesX86, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Choose a dedicated Iris subfolder, not the Program Files folder itself.");

        foreach (var special in new[]
                 {
                     Environment.SpecialFolder.UserProfile,
                     Environment.SpecialFolder.MyDocuments,
                     Environment.SpecialFolder.DesktopDirectory,
                     Environment.SpecialFolder.StartMenu,
                     Environment.SpecialFolder.LocalApplicationData,
                     Environment.SpecialFolder.ApplicationData
                 })
        {
            var folder = Environment.GetFolderPath(special);
            if (!string.IsNullOrWhiteSpace(folder) && fullPath.Equals(folder, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Choose a dedicated installation directory, not a Windows known folder itself.");
        }

        var runningRoot = Path.GetFullPath(AppContext.BaseDirectory);
        if (fullPath.StartsWith(runningRoot, StringComparison.OrdinalIgnoreCase) ||
            runningRoot.StartsWith(fullPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("The installation directory must not overlap the running installer package.");

        if (File.Exists(fullPath))
            throw new ArgumentException("The installation path is an existing file. Choose an empty or Iris-owned folder.");

        if (Directory.Exists(fullPath))
        {
            if (new DirectoryInfo(fullPath).Attributes.HasFlag(FileAttributes.ReparsePoint))
                throw new ArgumentException("The installation directory must not be a symlink or reparse point.");
            if (Directory.EnumerateFileSystemEntries(fullPath).Any() && !IsIrisOwnedDirectory(fullPath))
                throw new ArgumentException("The selected folder is not empty and does not contain an Iris installation. Choose an empty folder or the existing Iris folder.");
        }
        else
        {
            var parent = Path.GetDirectoryName(fullPath);
            if (parent is not null && Directory.Exists(parent) &&
                new DirectoryInfo(parent).Attributes.HasFlag(FileAttributes.ReparsePoint))
                throw new ArgumentException("The parent directory must not be a symlink or reparse point.");
        }
    }

    public static string ResolveUninstallRoot(string configuredDirectory)
    {
        var candidate = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, ".."));
        if (IsIrisOwnedDirectory(candidate))
            return candidate;
        var registered = ReadInstallLocation();
        if (registered is not null && IsIrisOwnedDirectory(registered))
            return registered;
        return Path.GetFullPath(configuredDirectory);
    }

    private static void ValidateUninstallRoot(string path)
    {
        if (!IsIrisOwnedDirectory(path))
            throw new InvalidOperationException("The selected folder does not contain a recognized Iris installation.");
    }

    private static bool IsIrisOwnedDirectory(string path) =>
        Directory.Exists(path) &&
        (File.Exists(Path.Combine(path, "App", "Iris.App.exe")) ||
         File.Exists(Path.Combine(path, "install-options.json")));

    private static string? ReadInstallLocation()
    {
        foreach (var hive in new[] { Registry.LocalMachine, Registry.CurrentUser })
        {
            using var key = hive.OpenSubKey(UninstallKey);
            if (key?.GetValue("InstallLocation") is string location &&
                !string.IsNullOrWhiteSpace(location))
                return location;
        }

        return null;
    }

    private static void DeleteOrThrow(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
            else if (Directory.Exists(path))
            {
                foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                    _ = MoveFileEx(file, null, MoveFileFlags.DelayUntilReboot);
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException exception)
        {
            throw new IOException($"Could not remove '{path}'. Close Iris and try again.", exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new UnauthorizedAccessException($"Administrator privileges may be required to remove '{path}'.", exception);
        }
    }

    private static string? ReadInstalledVersion()
    {
        foreach (var hive in new[] { Registry.LocalMachine, Registry.CurrentUser })
        {
            using var key = hive.OpenSubKey(UninstallKey);
            if (key?.GetValue("DisplayVersion") is string version)
                return version;
        }
        return null;
    }

    private static void RegisterUninstall(string destination, string version, bool machineWide)
    {
        var hive = machineWide ? Registry.LocalMachine : Registry.CurrentUser;
        using var key = hive.CreateSubKey(UninstallKey, writable: true);
        key.SetValue("DisplayName", "Iris");
        key.SetValue("DisplayVersion", version);
        key.SetValue("Publisher", "dotarma contributors");
        key.SetValue("InstallLocation", destination);
        key.SetValue("URLInfoAbout", "https://github.com/dotarma/iris");
        key.SetValue("UninstallString", $"\"{Path.Combine(destination, "Installer", "Iris.Installer.exe")}\" --uninstall");
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
    }

    private static void DeleteUninstallRegistration()
    {
        Registry.CurrentUser.DeleteSubKey(UninstallKey, throwOnMissingSubKey: false);
        try { Registry.LocalMachine.DeleteSubKey(UninstallKey, throwOnMissingSubKey: false); }
        catch (UnauthorizedAccessException) { }
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
            else if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static void CopyInstallerFiles(string source, string destination)
    {
        var sourceRoot = Path.GetFullPath(source);
        foreach (var file in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceRoot, file);
            if (relative.StartsWith("payload" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                relative.StartsWith("payload" + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                continue;
            var target = Path.Combine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: false);
        }
    }

    private static void ScheduleDirectoryDeletion(string path)
    {
        if (!Directory.Exists(path))
            return;
        foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            _ = MoveFileEx(file, null, MoveFileFlags.DelayUntilReboot);
        foreach (var directory in Directory.EnumerateDirectories(path, "*", SearchOption.AllDirectories)
                     .OrderByDescending(value => value.Length))
            _ = MoveFileEx(directory, null, MoveFileFlags.DelayUntilReboot);
        _ = MoveFileEx(path, null, MoveFileFlags.DelayUntilReboot);
    }

    [Flags]
    private enum MoveFileFlags : uint { DelayUntilReboot = 0x4 }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MoveFileEx(string existingFileName, string? newFileName, MoveFileFlags flags);
}

internal static class Shortcut
{
    public static void Create(string shortcutPath, string targetPath, string workingDirectory)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(shortcutPath)!);
        var link = (IShellLinkW)(object)new ShellLink();
        link.SetPath(targetPath);
        link.SetWorkingDirectory(workingDirectory);
        link.SetDescription("Iris Android Device Toolkit");
        ((IPersistFile)link).Save(shortcutPath, true);
        Marshal.FinalReleaseComObject(link);
    }

    [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
    private sealed class ShellLink { }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder file, int maximum, IntPtr data, uint flags);
        void GetIDList(out IntPtr idList);
        void SetIDList(IntPtr idList);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder name, int maximum);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string name);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder directory, int maximum);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string directory);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder arguments, int maximum);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string arguments);
        void GetHotKey(out short hotKey);
        void SetHotKey(short hotKey);
        void GetShowCmd(out int showCommand);
        void SetShowCmd(int showCommand);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder iconPath, int maximum, out int iconIndex);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string iconPath, int iconIndex);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string path, uint reserved);
        void Resolve(IntPtr window, uint flags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string path);
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("0000010b-0000-0000-C000-000000000046")]
    private interface IPersistFile
    {
        void GetClassID(out Guid classId);
        [PreserveSig] int IsDirty();
        void Load([MarshalAs(UnmanagedType.LPWStr)] string fileName, uint mode);
        void Save([MarshalAs(UnmanagedType.LPWStr)] string fileName, bool remember);
        void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string fileName);
        void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string fileName);
    }
}
