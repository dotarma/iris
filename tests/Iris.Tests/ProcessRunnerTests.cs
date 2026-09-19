using Iris.Core;
using Iris.Services;

namespace Iris.Tests;

public sealed class ProcessRunnerTests
{
    [Fact]
    public async Task Runner_CapturesOutputWithoutShell()
    {
        var runner = new ProcessRunner(new NullLog());
        var executable = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "where.exe");
        var result = await runner.RunAsync(new ProcessRequest(executable, ["where.exe"], TimeSpan.FromSeconds(10)));
        Assert.True(result.Succeeded);
        Assert.Contains("where.exe", result.StandardOutput, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Runner_RejectsRelativeExecutablePaths()
    {
        var runner = new ProcessRunner(new NullLog());
        await Assert.ThrowsAsync<ArgumentException>(() => runner.RunAsync(new ProcessRequest("cmd.exe", ["/c", "whoami"])));
    }

    private sealed class NullLog : ILogService
    {
        public string LogPath => string.Empty;
        public void Write(string level, string eventName, string message, Exception? exception = null) { }
        public Task<string> ExportAsync(string destinationPath, CancellationToken cancellationToken = default) => Task.FromResult(destinationPath);
    }
}
