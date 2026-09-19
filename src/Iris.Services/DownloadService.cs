using System.Net;
using System.Security.Cryptography;
using Iris.Core;

namespace Iris.Services;

public sealed class DownloadService(HttpClient client, ILogService logService) : IDownloadService
{
    public async Task<DownloadResult> DownloadAsync(
        DownloadRequest request,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);
        var destination = Path.GetFullPath(request.DestinationPath);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        if (File.Exists(destination))
            throw new IOException("The destination file already exists.");

        Exception? lastError = null;
        for (var attempt = 0; attempt <= request.MaxRetries; attempt++)
        {
            var temporary = destination + $".{Guid.NewGuid():N}.download";
            try
            {
                logService.Write("Information", "download.start", $"Downloading from {request.Source.Host}.");
                using var response = await client.GetAsync(request.Source, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                    .ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                var length = response.Content.Headers.ContentLength;
                await using var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                await using var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                    81920, FileOptions.Asynchronous | FileOptions.WriteThrough);
                var buffer = new byte[81920];
                long total = 0;
                int read;
                while ((read = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                    total += read;
                    if (length > 0)
                        progress?.Report((double)total / length.Value);
                }

                await output.FlushAsync(cancellationToken).ConfigureAwait(false);
                output.Close();
                var hash = await ComputeSha256Async(temporary, cancellationToken).ConfigureAwait(false);
                if (request.ExpectedSha256 is { } expected && !hash.Equals(expected, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("The downloaded file failed SHA-256 verification.");

                File.Move(temporary, destination);
                progress?.Report(1);
                logService.Write("Information", "download.complete", $"Downloaded {total} bytes from {request.Source.Host}.");
                return new DownloadResult(destination, total, hash);
            }
            catch (Exception exception) when (ShouldRetry(exception, attempt, request.MaxRetries, cancellationToken))
            {
                lastError = exception;
                logService.Write("Warning", "download.retry", $"Download attempt {attempt + 1} failed: {exception.Message}");
                TryDelete(temporary);
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                TryDelete(temporary);
                throw;
            }
        }

        throw new HttpRequestException("The download failed after all retry attempts.", lastError);
    }

    public async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken = default)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var bytes = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static void ValidateRequest(DownloadRequest request)
    {
        if (request.Source.Scheme != Uri.UriSchemeHttps)
            throw new ArgumentException("Downloads must use HTTPS.", nameof(request));
        if (request.MaxRetries is < 0 or > 5)
            throw new ArgumentOutOfRangeException(nameof(request), "Retry count must be between zero and five.");
        if (request.ExpectedSha256 is { } hash && (hash.Length != 64 || !hash.All(Uri.IsHexDigit)))
            throw new ArgumentException("Expected SHA-256 must contain 64 hexadecimal characters.", nameof(request));
        if (request.AllowedExtensions is { Count: > 0 } extensions &&
            !extensions.Contains(Path.GetExtension(request.DestinationPath), StringComparer.OrdinalIgnoreCase))
            throw new InvalidDataException("The download has an unexpected file extension.");
    }

    private static bool ShouldRetry(Exception exception, int attempt, int maxRetries, CancellationToken token) =>
        !token.IsCancellationRequested && attempt < maxRetries &&
        exception is HttpRequestException or IOException &&
        exception is not HttpRequestException { StatusCode: HttpStatusCode.NotFound or HttpStatusCode.Forbidden };

    private static void TryDelete(string path)
    {
        try { File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
