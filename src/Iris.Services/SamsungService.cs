using Iris.Core;

namespace Iris.Services;

public sealed class SamsungService(ISettingsService settings, IProcessRunner runner) : ISamsungService
{
    public Task<ProcessResult> ListDevicesAsync(IProgress<ProcessOutputLine>? progress = null,
        CancellationToken cancellationToken = default) =>
        runner.RunAsync(new ProcessRequest(ResolveBrokkr(), ["--list"], TimeSpan.FromSeconds(30)), progress,
            cancellationToken);

    public async Task<IReadOnlyList<BrokkrDeviceInfo>> GetOdinDevicesAsync(
        CancellationToken cancellationToken = default)
    {
        var result = await ListDevicesAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        return result.Succeeded ? ParseListOutput(result.StandardOutput) : [];
    }

    public Task<ProcessResult> RebootDownloadAsync(IProgress<ProcessOutputLine>? progress = null,
        CancellationToken cancellationToken = default) =>
        runner.RunAsync(new ProcessRequest(ResolveBrokkr(), ["--reboot-download"], TimeSpan.FromMinutes(5)),
            progress, cancellationToken);

    public static List<BrokkrDeviceInfo> ParseListOutput(string output)
    {
        var devices = new List<BrokkrDeviceInfo>();
        foreach (var rawLine in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            if (line.StartsWith("No Samsung devices", StringComparison.OrdinalIgnoreCase))
                continue;
            var parts = line.Split('\t');
            if (parts.Length < 2)
                continue;
            var sysname = parts[0].Trim();
            if (sysname.Length == 0)
                continue;
            devices.Add(new BrokkrDeviceInfo(sysname,
                parts[1].Trim().Equals("Odin Mode", StringComparison.OrdinalIgnoreCase)));
        }

        return devices;
    }

    public Task<ProcessResult> FlashAsync(SamsungFlashRequest request,
        IProgress<ProcessOutputLine>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Target);
        if (new[] { request.BlPath, request.ApPath, request.CpPath, request.CscPath, request.UserDataPath }
            .All(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Select at least one firmware file.", nameof(request));

        var arguments = new List<string> { "--target", request.Target };
        AddFile(arguments, "-b", request.BlPath);
        AddFile(arguments, "-a", request.ApPath);
        AddFile(arguments, "-c", request.CpPath);
        AddFile(arguments, "-s", request.CscPath);
        AddFile(arguments, "-u", request.UserDataPath);
        AddFile(arguments, "--use-pit", request.PitPath);
        if (!request.RebootAfterFlash)
            arguments.Add("--no-reboot");

        return runner.RunAsync(new ProcessRequest(ResolveBrokkr(), arguments), progress,
            cancellationToken);
    }

    private string ResolveBrokkr()
    {
        var path = settings.Current.BrokkrPath;
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path) || !File.Exists(path))
            throw new FileNotFoundException("Choose the installed brokkR executable in Settings.");
        if (!Path.GetFileName(path).Equals("brokkr.exe", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The selected executable must be named brokkr.exe.");
        return Path.GetFullPath(path);
    }

    private static void AddFile(List<string> arguments, string option, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException("A selected firmware file does not exist.", fullPath);
        arguments.Add(option);
        arguments.Add(fullPath);
    }
}
