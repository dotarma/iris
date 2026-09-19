using Iris.Core;
using Iris.Services;

namespace Iris.Tests;

public sealed class BrokkrListTests
{
    [Fact]
    public void ParseList_ParsesOdinAndNonOdinTargets()
    {
        const string output = "usb-001\tOdin Mode\r\nusb-002\tNot in Odin Mode\r\n";
        var devices = SamsungService.ParseListOutput(output);
        Assert.Equal(2, devices.Count);
        Assert.Equal(new BrokkrDeviceInfo("usb-001", true), devices[0]);
        Assert.Equal(new BrokkrDeviceInfo("usb-002", false), devices[1]);
    }

    [Fact]
    public void ParseList_IgnoresEmptyMessageAndMalformedLines()
    {
        const string output = "No Samsung devices found.\r\njust-a-sysname-without-tab\r\n\r\n";
        Assert.Empty(SamsungService.ParseListOutput(output));
    }

    [Fact]
    public async Task Flash_RejectsMissingTargetBeforeTouchingProcesses()
    {
        var service = new SamsungService(new StubSettings(), new BoomRunner());
        var request = new SamsungFlashRequest(string.Empty, null, "C:\\firmware\\ap.tar", null, null, null, null, true);
        await Assert.ThrowsAsync<ArgumentException>(() => service.FlashAsync(request));
    }

    private sealed class StubSettings : ISettingsService
    {
        public AppSettings Current { get; } = new();
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class BoomRunner : IProcessRunner
    {
        public Task<ProcessResult> RunAsync(ProcessRequest request, IProgress<ProcessOutputLine>? progress = null,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("No process execution was expected.");
    }
}
