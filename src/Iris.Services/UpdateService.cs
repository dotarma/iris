using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO.Compression;
using System.Security.Cryptography;
using Iris.Core;

namespace Iris.Services;

public sealed class UpdateService(
    HttpClient client,
    IDownloadService downloads,
    ISettingsService settings,
    ILogService logService) : IUpdateService
{
    public static readonly Uri MetadataUri = new(
        "https://raw.githubusercontent.com/dotarma/iris/update/update.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public async Task<UpdateCheckResult> CheckAsync(Version currentVersion,
        CancellationToken cancellationToken = default)
    {
        using var response = await client.GetAsync(MetadataUri, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var content = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var metadata = await JsonSerializer.DeserializeAsync<UpdateMetadata>(content, JsonOptions, cancellationToken)
            .ConfigureAwait(false) ?? throw new InvalidDataException("Update metadata is empty.");
        ValidateMetadata(metadata);

        var available = ParseVersion(metadata.Version, "version");
        var minimum = ParseVersion(metadata.MinimumVersion, "minimumVersion");
        if (currentVersion < minimum)
            throw new InvalidOperationException("This Iris version is too old for the published update path. Install the latest release manually.");
        if (available <= currentVersion)
            return new UpdateCheckResult(currentVersion, null, metadata, null);

        var key = ArchitectureInfo.MetadataKey(ArchitectureInfo.Current);
        if (!metadata.Architectures.TryGetValue(key, out var package))
            throw new PlatformNotSupportedException($"No update package is available for {key}.");
        ValidatePackage(package);
        return new UpdateCheckResult(currentVersion, available, metadata, package);
    }

    public async Task<string> DownloadAsync(UpdateCheckResult update, IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!update.IsUpdateAvailable || update.Package is null || update.AvailableVersion is null)
            throw new InvalidOperationException("No update is available to download.");
        ValidatePackage(update.Package);
        Directory.CreateDirectory(settings.Current.DownloadDirectory);
        var extension = Path.GetExtension(update.Package.Url.AbsolutePath).ToLowerInvariant();
        var destination = Path.Combine(settings.Current.DownloadDirectory,
            $"Iris-{update.AvailableVersion}-{ArchitectureInfo.MetadataKey(ArchitectureInfo.Current)}{extension}");
        if (File.Exists(destination))
            File.Delete(destination);
        var result = await downloads.DownloadAsync(new DownloadRequest(update.Package.Url, destination,
            update.Package.Sha256, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".zip" }),
            progress, cancellationToken).ConfigureAwait(false);
        logService.Write("Information", "update.download", $"Update {update.AvailableVersion} verified and ready for user launch.");
        return await PrepareArchiveAsync(result.Path, update.AvailableVersion, cancellationToken).ConfigureAwait(false);
    }

    public static void ValidateMetadata(UpdateMetadata metadata)
    {
        _ = ParseVersion(metadata.Version, "version");
        _ = ParseVersion(metadata.MinimumVersion, "minimumVersion");
        if (metadata.Channel is not ("update" or "stable"))
            throw new InvalidDataException("The update channel is invalid.");
        if (metadata.ReleaseDate > DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)))
            throw new InvalidDataException("The update release date is invalid.");
        if (string.IsNullOrWhiteSpace(metadata.ReleaseNotes) || metadata.ReleaseNotes.Length > 100_000)
            throw new InvalidDataException("The release notes are invalid.");
        if (metadata.Architectures.Count is 0 or > 3 ||
            metadata.Architectures.Keys.Any(key => key is not ("x86" or "x64" or "arm64")))
            throw new InvalidDataException("Update architecture metadata is invalid.");
        foreach (var package in metadata.Architectures.Values)
            ValidatePackage(package);
    }

    private static Version ParseVersion(string value, string property)
    {
        if (!Version.TryParse(value, out var version) || version.Major < 0 || version.Build < 0 || version.Revision >= 0)
            throw new InvalidDataException($"Update {property} must use MAJOR.MINOR.PATCH.");
        return version;
    }

    private static void ValidatePackage(UpdatePackage package)
    {
        if (package.Url.Scheme != Uri.UriSchemeHttps || !package.Url.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The update package source is not trusted.");
        if (!package.Url.AbsolutePath.StartsWith("/dotarma/iris/releases/download/", StringComparison.Ordinal) ||
            !string.Equals(Path.GetExtension(package.Url.AbsolutePath), ".zip", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The update package type is unsupported.");
        if (package.Sha256.Length != 64 || !package.Sha256.All(Uri.IsHexDigit))
            throw new InvalidDataException("The update package SHA-256 is invalid.");
    }

    private static async Task<string> PrepareArchiveAsync(string archivePath, Version expectedVersion,
        CancellationToken cancellationToken)
    {
        var prepared = Path.Combine(Path.GetDirectoryName(archivePath)!, Path.GetFileNameWithoutExtension(archivePath));
        if (Directory.Exists(prepared))
            Directory.Delete(prepared, recursive: true);
        Directory.CreateDirectory(prepared);
        var preparedRoot = Path.GetFullPath(prepared) + Path.DirectorySeparatorChar;
        try
        {
            using var archive = ZipFile.OpenRead(archivePath);
            if (archive.Entries.Count is 0 or > 15000 || archive.Entries.Sum(entry => entry.Length) > 2L * 1024 * 1024 * 1024)
                throw new InvalidDataException("The update archive has unreasonable contents.");
            foreach (var entry in archive.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var target = Path.GetFullPath(Path.Combine(prepared, entry.FullName));
                if (!target.StartsWith(preparedRoot, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("The update archive contains an unsafe path.");
                if (string.IsNullOrEmpty(entry.Name))
                {
                    Directory.CreateDirectory(target);
                    continue;
                }
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                entry.ExtractToFile(target, overwrite: false);
            }

            var manifestPath = Path.Combine(prepared, "package.manifest.json");
            if (!File.Exists(manifestPath))
                throw new InvalidDataException("The update package manifest is missing.");
            var manifest = JsonSerializer.Deserialize<PackageManifest>(
                await File.ReadAllTextAsync(manifestPath, cancellationToken).ConfigureAwait(false), JsonOptions)
                ?? throw new InvalidDataException("The update package manifest is invalid.");
            if (!Version.TryParse(manifest.Version, out var version) || version != expectedVersion)
                throw new InvalidDataException("The package version does not match update metadata.");
            if (!manifest.Architecture.Equals(ArchitectureInfo.MetadataKey(ArchitectureInfo.Current), StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The package architecture does not match this process.");
            var actualFiles = Directory.EnumerateFiles(prepared, "*", SearchOption.AllDirectories)
                .Where(path => !path.Equals(manifestPath, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (manifest.Files.Count == 0 || manifest.Files.Count != actualFiles.Length)
                throw new InvalidDataException("The package file list is incomplete.");
            foreach (var pair in manifest.Files)
            {
                var file = Path.GetFullPath(Path.Combine(prepared, pair.Key.Replace('/', Path.DirectorySeparatorChar)));
                if (!file.StartsWith(preparedRoot, StringComparison.OrdinalIgnoreCase) || !File.Exists(file) ||
                    pair.Value.Length != 64 || !pair.Value.All(Uri.IsHexDigit))
                    throw new InvalidDataException("The package manifest contains an invalid file entry.");
                await using var stream = File.OpenRead(file);
                var actualHash = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false));
                if (!actualHash.Equals(pair.Value, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException($"Update file integrity verification failed for {Path.GetFileName(file)}.");
            }

            var installer = Path.Combine(prepared, "Iris.Installer.exe");
            if (!File.Exists(installer))
                throw new InvalidDataException("The update does not contain Iris.Installer.exe.");
            return installer;
        }
        catch
        {
            if (Directory.Exists(prepared))
                Directory.Delete(prepared, recursive: true);
            throw;
        }
    }

    private sealed class PackageManifest
    {
        public required string Version { get; init; }
        public required string Architecture { get; init; }
        public required Dictionary<string, string> Files { get; init; }
    }
}
