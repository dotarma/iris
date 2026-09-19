using Iris.Core;
using Microsoft.Win32;

namespace Iris.Services;

public sealed class DriverManager : IDriverManager
{
    private static readonly Uri AndroidDrivers = new("https://developer.android.com/studio/run/oem-usb");
    private static readonly Uri SamsungDrivers = new("https://developer.samsung.com/android-usb-driver");

    public Task<IReadOnlyList<DriverInfo>> DetectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var installed = ReadInstalledPrograms();
        IReadOnlyList<DriverInfo> result =
        [
            Create("Samsung Android USB Driver", "Samsung", SamsungDrivers, installed, ["samsung", "usb", "driver"],
                "Download and run Samsung's installer interactively. Iris never bundles or silently installs it."),
            Create("Android USB / Fastboot Driver", "Device manufacturer / Google OEM guidance", AndroidDrivers,
                installed, ["android", "usb", "driver"],
                "Use your device manufacturer's signed driver. Driver Signature Enforcement remains enabled.")
        ];
        return Task.FromResult(result);
    }

    private static DriverInfo Create(string name, string source, Uri uri,
        IReadOnlyList<(string Name, string? Version)> programs, IReadOnlyList<string> terms, string note)
    {
        var match = programs.FirstOrDefault(program => terms.All(term =>
            program.Name.Contains(term, StringComparison.OrdinalIgnoreCase)));
        return new DriverInfo(name, source, uri, match.Name is not null, match.Version, true, note);
    }

    private static List<(string Name, string? Version)> ReadInstalledPrograms()
    {
        var programs = new List<(string, string?)>();
        foreach (var hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
        foreach (var view in new[] { RegistryView.Registry32, RegistryView.Registry64 })
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                using var uninstall = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
                if (uninstall is null)
                    continue;
                foreach (var keyName in uninstall.GetSubKeyNames())
                {
                    using var item = uninstall.OpenSubKey(keyName);
                    if (item?.GetValue("DisplayName") is string displayName)
                        programs.Add((displayName, item.GetValue("DisplayVersion") as string));
                }
            }
            catch (UnauthorizedAccessException) { }
            catch (System.Security.SecurityException) { }
        }

        return programs;
    }
}
