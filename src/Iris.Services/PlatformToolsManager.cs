using System.IO.Compression;
using Iris.Core;

namespace Iris.Services;

public sealed class PlatformToolsManager(
    ISettingsService settings,
    IDownloadService downloadService,
    IProcessRunner processRunner,
    ILogService logService) : IPlatformToolsManager
{
    private const long MaximumExpandedBytes = 512L * 1024 * 1024;

    public string? ResolveAdbPath() => ResolveExecutable("adb.exe");
    public string? ResolveFastbootPath() => ResolveExecutable("fastboot.exe");

    public async Task<ToolStatus> DetectAsync(CancellationToken cancellationToken = default)
    {
        var adbPath = ResolveAdbPath();
        var fastbootPath = ResolveFastbootPath();
        var adbVersion = await ReadVersionAsync(adbPath, ["version"], cancellationToken).ConfigureAwait(false);
        var fastbootVersion = await ReadVersionAsync(fastbootPath, ["--version"], cancellationToken).ConfigureAwait(false);
        return new ToolStatus(adbPath is not null, fastbootPath is not null, adbPath, fastbootPath,
            adbVersion, fastbootVersion);
    }

    public async Task InstallAsync(
        Uri officialArchive,
        string? expectedSha256,
        bool replaceExisting,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!settings.Current.PlatformToolsLicenseAccepted)
            throw new InvalidOperationException("Accept the Android SDK license before downloading platform-tools.");
        if (officialArchive.Scheme != Uri.UriSchemeHttps || !officialArchive.Host.Equals("dl.google.com", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Platform-tools may only be downloaded from dl.google.com.");

        var root = Path.GetFullPath(settings.Current.ToolDirectory);
        var destination = Path.Combine(root, "platform-tools");
        if (Directory.Exists(destination) && !replaceExisting)
            throw new IOException("Platform-tools is already installed. Choose Update to replace it.");

        Directory.CreateDirectory(root);
        var archivePath = Path.Combine(Path.GetTempPath(), $"iris-platform-tools-{Guid.NewGuid():N}.zip");
        var staging = Path.Combine(root, $".platform-tools-{Guid.NewGuid():N}.staging");
        var backup = destination + $".{Guid.NewGuid():N}.backup";
        try
        {
            await downloadService.DownloadAsync(new DownloadRequest(
                officialArchive, archivePath, expectedSha256,
                new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".zip" }), progress: progress,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            ExtractSafely(archivePath, staging, cancellationToken);
            var extracted = Path.Combine(staging, "platform-tools");
            if (!File.Exists(Path.Combine(extracted, "adb.exe")) || !File.Exists(Path.Combine(extracted, "fastboot.exe")))
                throw new InvalidDataException("The archive does not contain the expected platform-tools executables.");

            if (Directory.Exists(destination))
                Directory.Move(destination, backup);
            try
            {
                Directory.Move(extracted, destination);
                if (Directory.Exists(backup))
                    Directory.Delete(backup, recursive: true);
            }
            catch
            {
                if (!Directory.Exists(destination) && Directory.Exists(backup))
                    Directory.Move(backup, destination);
                throw;
            }

            logService.Write("Information", "platform-tools.install", "Platform-tools installed successfully.");
        }
        finally
        {
            TryDeleteFile(archivePath);
            TryDeleteDirectory(staging);
        }
    }

    private string? ResolveExecutable(string fileName)
    {
        var configured = Path.GetFullPath(settings.Current.ToolDirectory);
        var candidates = new[]
        {
            Path.Combine(configured, "platform-tools", fileName),
            Path.Combine(configured, fileName)
        };
        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
                return candidate;
        }

        foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
                     .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            try
            {
                var candidate = Path.Combine(directory.Trim('"'), fileName);
                if (File.Exists(candidate))
                    return Path.GetFullPath(candidate);
            }
            catch (ArgumentException) { }
        }

        return null;
    }

    private async Task<string?> ReadVersionAsync(string? path, IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        if (path is null)
            return null;
        var result = await processRunner.RunAsync(new ProcessRequest(path, arguments, TimeSpan.FromSeconds(10)),
            cancellationToken: cancellationToken).ConfigureAwait(false);
        var text = string.Concat(result.StandardOutput, Environment.NewLine, result.StandardError).Trim();
        return result.Succeeded ? text.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() : null;
    }

    private static void ExtractSafely(string archivePath, string staging, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(staging);
        var stagingRoot = Path.GetFullPath(staging) + Path.DirectorySeparatorChar;
        using var archive = ZipFile.OpenRead(archivePath);
        if (archive.Entries.Count is 0 or > 5000)
            throw new InvalidDataException("The archive contains an invalid number of entries.");
        if (archive.Entries.Sum(entry => entry.Length) > MaximumExpandedBytes)
            throw new InvalidDataException("The expanded archive is unexpectedly large.");

        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var target = Path.GetFullPath(Path.Combine(staging, entry.FullName));
            if (!target.StartsWith(stagingRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The archive contains an unsafe path.");
            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(target);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            entry.ExtractToFile(target, overwrite: false);
        }
    }

    private static void TryDeleteFile(string path)
    {
        try { File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static void TryDeleteDirectory(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
