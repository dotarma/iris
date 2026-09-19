using System.Diagnostics;
using System.Text;
using Iris.Core;

namespace Iris.Services;

public sealed class ProcessRunner(ILogService logService) : IProcessRunner
{
    private const int MaxOutputCharsPerStream = 262_144;
    private static readonly TimeSpan CancelDrainTimeout = TimeSpan.FromSeconds(5);

    public async Task<ProcessResult> RunAsync(
        ProcessRequest request,
        IProgress<ProcessOutputLine>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.FileName);
        if (!Path.IsPathFullyQualified(request.FileName))
            throw new ArgumentException("Executable paths must be absolute.", nameof(request));

        var startInfo = new ProcessStartInfo
        {
            FileName = request.FileName,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = request.WorkingDirectory ?? Path.GetDirectoryName(request.FileName)!
        };

        foreach (var argument in request.Arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        var stopwatch = Stopwatch.StartNew();
        logService.Write("Information", "process.start", $"Starting {Path.GetFileName(request.FileName)} with {request.Arguments.Count} argument(s).");

        if (!process.Start())
            throw new InvalidOperationException($"Could not start {Path.GetFileName(request.FileName)}.");

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        var outputTask = ReadLinesAsync(process.StandardOutput, false, stdout, progress);
        var errorTask = ReadLinesAsync(process.StandardError, true, stderr, progress);
        using var timeout = request.Timeout is { } duration
            ? new CancellationTokenSource(duration)
            : new CancellationTokenSource();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

        var cancelled = false;
        var timedOut = false;
        try
        {
            await process.WaitForExitAsync(linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            timedOut = timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested;
            cancelled = !timedOut;
            TryKill(process);
        }

        try
        {
            await Task.WhenAll(outputTask, errorTask).WaitAsync(CancelDrainTimeout, CancellationToken.None).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            logService.Write("Warning", "process.drain-timeout",
                $"Output streams for {Path.GetFileName(request.FileName)} did not close after cancellation.");
        }

        stopwatch.Stop();
        var exitCode = process.HasExited ? process.ExitCode : -1;
        var result = new ProcessResult(exitCode, stdout.ToString(), stderr.ToString(), stopwatch.Elapsed, cancelled, timedOut);
        logService.Write(result.Succeeded ? "Information" : "Warning", "process.exit",
            $"{Path.GetFileName(request.FileName)} exited with code {exitCode} after {stopwatch.Elapsed.TotalMilliseconds:F0} ms.");
        return result;
    }

    private static async Task ReadLinesAsync(
        StreamReader reader,
        bool isError,
        StringBuilder destination,
        IProgress<ProcessOutputLine>? progress)
    {
        var truncated = false;
        while (await reader.ReadLineAsync().ConfigureAwait(false) is { } line)
        {
            if (destination.Length < MaxOutputCharsPerStream)
            {
                destination.AppendLine(line);
            }
            else if (!truncated)
            {
                destination.AppendLine("[Output truncated: limit reached]");
                truncated = true;
            }

            progress?.Report(new ProcessOutputLine(DateTimeOffset.Now, isError, line));
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException) { }
        catch (System.ComponentModel.Win32Exception) { }
    }
}
