using System.IO.Compression;
using Iris.Core;
using Iris.Services;

namespace Iris.Tests;

public sealed class PlatformToolsSecurityTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "Iris.PlatformTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Install_RejectsArchivePathTraversal()
    {
        Directory.CreateDirectory(_root);
        var archive = Path.Combine(_root, "source.zip");
        using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create))
            zip.CreateEntry("../escaped.exe");
        var settings = new TestSettings(Path.Combine(_root, "tools"));
        var manager = new PlatformToolsManager(settings, new CopyDownload(archive), new NeverRunProcess(), new NullLog());
        await Assert.ThrowsAsync<InvalidDataException>(() => manager.InstallAsync(
            new Uri("https://dl.google.com/android/repository/platform-tools.zip"), null, false));
        Assert.False(File.Exists(Path.Combine(_root, "escaped.exe")));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private sealed class TestSettings : ISettingsService
    {
        public TestSettings(string toolDirectory) => Current = new AppSettings
        {
            ToolDirectory = toolDirectory,
            PlatformToolsLicenseAccepted = true
        };
        public AppSettings Current { get; }
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class CopyDownload(string source) : IDownloadService
    {
        public Task<DownloadResult> DownloadAsync(DownloadRequest request, IProgress<double>? progress = null,
            CancellationToken cancellationToken = default)
        {
            File.Copy(source, request.DestinationPath);
            return Task.FromResult(new DownloadResult(request.DestinationPath, new FileInfo(source).Length, new string('0', 64)));
        }
        public Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken = default) => Task.FromResult(new string('0', 64));
    }

    private sealed class NeverRunProcess : IProcessRunner
    {
        public Task<ProcessResult> RunAsync(ProcessRequest request, IProgress<ProcessOutputLine>? progress = null,
            CancellationToken cancellationToken = default) => throw new InvalidOperationException("Process execution was not expected.");
    }

    private sealed class NullLog : ILogService
    {
        public string LogPath => string.Empty;
        public void Write(string level, string eventName, string message, Exception? exception = null) { }
        public Task<string> ExportAsync(string destinationPath, CancellationToken cancellationToken = default) => Task.FromResult(destinationPath);
    }
}
