using System.Text.RegularExpressions;
using Iris.Core;

namespace Iris.Services;

public sealed partial class AdbService(IPlatformToolsManager tools, IProcessRunner runner) : IAdbService
{
    public async Task<string?> GetVersionAsync(CancellationToken cancellationToken = default)
    {
        var result = await ExecuteAsync(["version"], cancellationToken: cancellationToken).ConfigureAwait(false);
        return result.Succeeded ? result.StandardOutput.Trim() : null;
    }

    public async Task<IReadOnlyList<DeviceInfo>> GetDevicesAsync(CancellationToken cancellationToken = default)
    {
        var result = await ExecuteAsync(["devices", "-l"], cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
            return [];
        return ParseDevices(result.StandardOutput);
    }

    public Task<ProcessResult> ExecuteAsync(IReadOnlyList<string> arguments, string? serial = null,
        IProgress<ProcessOutputLine>? progress = null, CancellationToken cancellationToken = default)
    {
        var path = tools.ResolveAdbPath() ?? throw new FileNotFoundException(
            "ADB is unavailable. Install platform-tools or choose its directory in Settings.");
        var effectiveArguments = string.IsNullOrWhiteSpace(serial)
            ? arguments
            : new[] { "-s", serial }.Concat(arguments).ToArray();
        return runner.RunAsync(new ProcessRequest(path, effectiveArguments, TimeSpan.FromMinutes(10)), progress,
            cancellationToken);
    }

    public Task<ProcessResult> RebootAsync(string serial, string? mode = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serial);
        var arguments = string.IsNullOrWhiteSpace(mode) ? new[] { "reboot" } : new[] { "reboot", mode };
        return ExecuteAsync(arguments, serial, cancellationToken: cancellationToken);
    }

    public static IReadOnlyList<DeviceInfo> ParseDevices(string output)
    {
        var devices = new List<DeviceInfo>();
        foreach (var rawLine in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            if (line.StartsWith("List of devices", StringComparison.OrdinalIgnoreCase) || line.StartsWith('*'))
                continue;
            var match = AdbDeviceLine().Match(line);
            if (!match.Success)
                continue;
            var state = ParseAdbState(match.Groups["state"].Value);
            var details = match.Groups["details"].Value;
            devices.Add(new DeviceInfo(
                match.Groups["serial"].Value,
                DeviceTransport.Adb,
                state,
                ReadProperty(details, "product"),
                ReadProperty(details, "model")?.Replace('_', ' '),
                ReadProperty(details, "transport_id") is null ? "USB or network" : "ADB"));
        }

        return devices;
    }

    private static string? ReadProperty(string details, string property)
    {
        var prefix = property + ":";
        return details.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(part => part.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))?[prefix.Length..];
    }

    private static DeviceState ParseAdbState(string state) => state.ToLowerInvariant() switch
    {
        "device" => DeviceState.Device,
        "unauthorized" => DeviceState.Unauthorized,
        "offline" => DeviceState.Offline,
        "recovery" => DeviceState.Recovery,
        "bootloader" => DeviceState.Bootloader,
        _ => DeviceState.Unknown
    };

    [GeneratedRegex("^(?<serial>\\S+)\\s+(?<state>\\S+)(?:\\s+(?<details>.*))?$")]
    private static partial Regex AdbDeviceLine();
}

public sealed class FastbootService(IPlatformToolsManager tools, IProcessRunner runner) : IFastbootService
{
    public async Task<string?> GetVersionAsync(CancellationToken cancellationToken = default)
    {
        var result = await ExecuteAsync(["--version"], cancellationToken: cancellationToken).ConfigureAwait(false);
        var text = string.Concat(result.StandardOutput, Environment.NewLine, result.StandardError).Trim();
        return result.Succeeded ? text : null;
    }

    public async Task<IReadOnlyList<DeviceInfo>> GetDevicesAsync(CancellationToken cancellationToken = default)
    {
        var result = await ExecuteAsync(["devices"], cancellationToken: cancellationToken).ConfigureAwait(false);
        return result.Succeeded ? ParseDevices(result.StandardOutput) : [];
    }

    public Task<ProcessResult> ExecuteAsync(IReadOnlyList<string> arguments, string? serial = null,
        IProgress<ProcessOutputLine>? progress = null, CancellationToken cancellationToken = default)
    {
        var path = tools.ResolveFastbootPath() ?? throw new FileNotFoundException(
            "Fastboot is unavailable. Install platform-tools or choose its directory in Settings.");
        var effectiveArguments = string.IsNullOrWhiteSpace(serial)
            ? arguments
            : new[] { "-s", serial }.Concat(arguments).ToArray();
        return runner.RunAsync(new ProcessRequest(path, effectiveArguments, TimeSpan.FromMinutes(10)), progress,
            cancellationToken);
    }

    public Task<ProcessResult> RebootAsync(string serial, bool bootloader = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serial);
        return ExecuteAsync(bootloader ? ["reboot", "bootloader"] : ["reboot"], serial,
            cancellationToken: cancellationToken);
    }

    public static IReadOnlyList<DeviceInfo> ParseDevices(string output) => output
        .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
        .Select(line => line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        .Where(parts => parts.Length >= 1)
        .Select(parts => new DeviceInfo(parts[0], DeviceTransport.Fastboot, DeviceState.Fastboot,
            Connection: parts.Length > 1 ? parts[1] : "fastboot"))
        .ToArray();
}
