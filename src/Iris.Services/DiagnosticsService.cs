using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Iris.Core;

namespace Iris.Services;

public sealed class DiagnosticsService(
    IPlatformToolsManager tools,
    IAdbService adb,
    IFastbootService fastboot,
    IDriverManager drivers) : IDiagnosticsService
{
    public async Task<DiagnosticReport> CreateAsync(CancellationToken cancellationToken = default)
    {
        var toolStatusTask = tools.DetectAsync(cancellationToken);
        var adbDevicesTask = SafeCountAsync(() => adb.GetDevicesAsync(cancellationToken));
        var fastbootDevicesTask = SafeCountAsync(() => fastboot.GetDevicesAsync(cancellationToken));
        var driverTask = drivers.DetectAsync(cancellationToken);
        await Task.WhenAll(toolStatusTask, adbDevicesTask, fastbootDevicesTask, driverTask).ConfigureAwait(false);
        var status = await toolStatusTask.ConfigureAwait(false);
        var driverList = await driverTask.ConfigureAwait(false);
        var version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "unknown";
        var values = new Dictionary<string, string>
        {
            ["Iris version"] = version,
            ["Windows"] = RuntimeInformation.OSDescription,
            ["Process architecture"] = RuntimeInformation.ProcessArchitecture.ToString(),
            ["OS architecture"] = RuntimeInformation.OSArchitecture.ToString(),
            [".NET"] = RuntimeInformation.FrameworkDescription,
            ["ADB"] = status.AdbVersion ?? "Not available",
            ["Fastboot"] = status.FastbootVersion ?? "Not available",
            ["ADB devices"] = (await adbDevicesTask.ConfigureAwait(false)).ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["Fastboot devices"] = (await fastbootDevicesTask.ConfigureAwait(false)).ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["Drivers"] = string.Join(", ", driverList.Select(driver => $"{driver.Name}: {(driver.IsInstalled ? driver.Version ?? "installed" : "not detected")}"))
        };
        return new DiagnosticReport(DateTimeOffset.UtcNow, values);
    }

    public async Task ExportAsync(DiagnosticReport report, string destinationPath,
        CancellationToken cancellationToken = default)
    {
        var fullPath = Path.GetFullPath(destinationPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllTextAsync(fullPath, report.ToString(), new UTF8Encoding(false), cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<int> SafeCountAsync(Func<Task<IReadOnlyList<DeviceInfo>>> operation)
    {
        try { return (await operation().ConfigureAwait(false)).Count; }
        catch (IOException) { return 0; }
        catch (InvalidOperationException) { return 0; }
    }
}
