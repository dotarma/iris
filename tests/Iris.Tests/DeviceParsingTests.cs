using Iris.Core;
using Iris.Services;

namespace Iris.Tests;

public sealed class DeviceParsingTests
{
    [Fact]
    public void AdbParser_ParsesStatesAndDetails()
    {
        const string output = "List of devices attached\r\nR58M123 device product:gts model:Galaxy_Tab transport_id:1\r\nABC unauthorized\r\nOFF offline\r\n";
        var devices = AdbService.ParseDevices(output);
        Assert.Equal(3, devices.Count);
        Assert.Equal("Galaxy Tab", devices[0].Model);
        Assert.Equal("gts", devices[0].Product);
        Assert.Equal(DeviceState.Unauthorized, devices[1].State);
        Assert.Equal(DeviceState.Offline, devices[2].State);
    }

    [Fact]
    public void FastbootParser_ParsesMultipleDevices()
    {
        var devices = FastbootService.ParseDevices("ABC\tfastboot\r\nDEF fastboot\r\n");
        Assert.Equal(2, devices.Count);
        Assert.All(devices, device => Assert.Equal(DeviceTransport.Fastboot, device.Transport));
    }
}
