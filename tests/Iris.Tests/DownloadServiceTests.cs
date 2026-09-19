using System.Net;
using System.Security.Cryptography;
using Iris.Core;
using Iris.Services;

namespace Iris.Tests;

public sealed class DownloadServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "Iris.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Download_VerifiesChecksumAndFinalizesAtomically()
    {
        var content = "verified content"u8.ToArray();
        var hash = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        using var client = new HttpClient(new StaticHandler(content));
        var service = new DownloadService(client, new NullLog());
        var destination = Path.Combine(_directory, "payload.exe");
        var result = await service.DownloadAsync(new DownloadRequest(new Uri("https://example.test/payload.exe"),
            destination, hash, new HashSet<string> { ".exe" }, 0));
        Assert.Equal(hash, result.Sha256);
        Assert.Equal(content, await File.ReadAllBytesAsync(destination));
        Assert.Empty(Directory.GetFiles(_directory, "*.download"));
    }

    [Fact]
    public async Task Download_DeletesTemporaryFileOnChecksumMismatch()
    {
        Directory.CreateDirectory(_directory);
        using var client = new HttpClient(new StaticHandler("bad"u8.ToArray()));
        var service = new DownloadService(client, new NullLog());
        var destination = Path.Combine(_directory, "payload.exe");
        await Assert.ThrowsAsync<InvalidDataException>(() => service.DownloadAsync(new DownloadRequest(
            new Uri("https://example.test/payload.exe"), destination, new string('0', 64),
            new HashSet<string> { ".exe" }, 0)));
        Assert.False(File.Exists(destination));
        Assert.Empty(Directory.GetFiles(_directory));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    private sealed class StaticHandler(byte[] content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(content)
        });
    }

    private sealed class NullLog : ILogService
    {
        public string LogPath => Path.GetTempFileName();
        public void Write(string level, string eventName, string message, Exception? exception = null) { }
        public Task<string> ExportAsync(string destinationPath, CancellationToken cancellationToken = default) =>
            Task.FromResult(destinationPath);
    }
}
