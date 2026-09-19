using Iris.Core;
using Iris.Services;

namespace Iris.Tests;

public sealed class CommandSafetyTests
{
    private readonly RestrictedCommandParser _parser = new();

    [Theory]
    [InlineData("adb devices")]
    [InlineData("adb version")]
    [InlineData("adb get-state")]
    [InlineData("adb get-serialno")]
    [InlineData("adb shell getprop")]
    [InlineData("fastboot devices")]
    [InlineData("fastboot --version")]
    [InlineData("fastboot getvar product")]
    public void ReadOnlyCommands_DoNotRequireConfirmation(string command)
    {
        Assert.False(_parser.Parse(command).ChangesDeviceState);
    }

    [Theory]
    [InlineData("adb reboot")]
    [InlineData("adb reboot recovery")]
    [InlineData("adb shell")]
    [InlineData("adb shell rm -rf /sdcard")]
    [InlineData("adb install app.apk")]
    [InlineData("adb push local /sdcard")]
    [InlineData("adb root")]
    [InlineData("fastboot reboot")]
    [InlineData("fastboot flashing unlock")]
    [InlineData("fastboot oem unlock")]
    [InlineData("fastboot flash boot boot.img")]
    public void StateChangingCommands_RequireConfirmation(string command)
    {
        Assert.True(_parser.Parse(command).ChangesDeviceState);
    }

    [Fact]
    public void Architecture_UsesProcessArchitecture()
    {
        var expected = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture switch
        {
            System.Runtime.InteropServices.Architecture.X86 => IrisArchitecture.X86,
            System.Runtime.InteropServices.Architecture.X64 => IrisArchitecture.X64,
            System.Runtime.InteropServices.Architecture.Arm64 => IrisArchitecture.Arm64,
            _ => throw new PlatformNotSupportedException("Test host architecture is unsupported."),
        };
        Assert.Equal(expected, ArchitectureInfo.Current);
    }
}
