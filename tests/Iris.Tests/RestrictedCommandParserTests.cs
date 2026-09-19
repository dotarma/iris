using Iris.Services;

namespace Iris.Tests;

public sealed class RestrictedCommandParserTests
{
    private readonly RestrictedCommandParser _parser = new();

    [Theory]
    [InlineData("adb devices", "adb", 1)]
    [InlineData("adb shell getprop", "adb", 2)]
    [InlineData("adb reboot recovery", "adb", 2)]
    [InlineData("fastboot devices", "fastboot", 1)]
    [InlineData("fastboot getvar product", "fastboot", 2)]
    [InlineData("fastboot reboot", "fastboot", 1)]
    [InlineData("adb push \"C:\\My Files\\image.zip\" /sdcard/", "adb", 3)]
    public void Parse_AllowsOnlyExpectedExecutables(string command, string executable, int argumentCount)
    {
        var result = _parser.Parse(command);
        Assert.Equal(executable, result.Executable);
        Assert.Equal(argumentCount, result.Arguments.Count);
    }

    [Theory]
    [InlineData("adb devices & whoami")]
    [InlineData("adb devices && whoami")]
    [InlineData("adb devices | whoami")]
    [InlineData("adb devices > output.txt")]
    [InlineData("adb devices && powershell -NoProfile")]
    [InlineData("powershell -NoProfile")]
    [InlineData("cmd /c whoami")]
    [InlineData("bash -c whoami")]
    [InlineData("./malicious.exe")]
    [InlineData("..\\malicious.exe")]
    [InlineData("C:\\malicious.exe")]
    [InlineData("adb shell \"getprop & whoami\"")]
    [InlineData("adb shell $(whoami)")]
    [InlineData("adb shell %COMSPEC%")]
    [InlineData("adb -s serial reboot")]
    [InlineData("fastboot --serial serial reboot")]
    [InlineData("adb -H 127.0.0.1 devices")]
    [InlineData("adb -P 5037 devices")]
    [InlineData("adb -L tcp:1234 devices")]
    [InlineData("adb -t 1 shell getprop")]
    [InlineData("adb -d shell getprop")]
    [InlineData("adb -e shell getprop")]
    [InlineData("adb --transport-id=1 shell getprop")]
    [InlineData("adb devices\r\nwhoami")]
    [InlineData("adb \"unterminated")]
    public void Parse_RejectsInjectionAndArbitraryExecutables(string command) =>
        Assert.Throws<FormatException>(() => _parser.Parse(command));
}
