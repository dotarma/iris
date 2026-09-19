using System.Text.Json;
using Iris.Core;

namespace Iris.Services;

public sealed class SettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _path;

    public SettingsService()
    {
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Iris");
        _path = Path.Combine(directory, "settings.json");
    }

    public AppSettings Current { get; private set; } = new();

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_path))
            return;

        await using var stream = File.OpenRead(_path);
        Current = await JsonSerializer.DeserializeAsync<AppSettings>(stream, JsonOptions, cancellationToken)
            .ConfigureAwait(false) ?? new AppSettings();
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(_path)!;
        Directory.CreateDirectory(directory);
        var temporary = _path + ".tmp";
        await using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None,
                         4096, FileOptions.Asynchronous | FileOptions.WriteThrough))
        {
            await JsonSerializer.SerializeAsync(stream, Current, JsonOptions, cancellationToken).ConfigureAwait(false);
        }

        File.Move(temporary, _path, overwrite: true);
    }
}

public sealed class JsonLogService : ILogService
{
    private readonly object _gate = new();

    public JsonLogService()
    {
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Iris", "logs");
        Directory.CreateDirectory(directory);
        LogPath = Path.Combine(directory, $"iris-{DateTime.UtcNow:yyyyMMdd}.jsonl");
    }

    public string LogPath { get; }

    public void Write(string level, string eventName, string message, Exception? exception = null)
    {
        var entry = JsonSerializer.Serialize(new
        {
            timestamp = DateTimeOffset.UtcNow,
            level,
            eventName,
            message,
            exception = exception?.GetType().Name,
            exceptionMessage = exception?.Message
        });

        lock (_gate)
            File.AppendAllText(LogPath, entry + Environment.NewLine);
    }

    public async Task<string> ExportAsync(string destinationPath, CancellationToken cancellationToken = default)
    {
        var fullPath = Path.GetFullPath(destinationPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await using var source = new FileStream(LogPath, FileMode.OpenOrCreate, FileAccess.Read, FileShare.ReadWrite,
            81920, FileOptions.Asynchronous);
        await using var destination = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
            81920, FileOptions.Asynchronous);
        await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
        return fullPath;
    }
}
