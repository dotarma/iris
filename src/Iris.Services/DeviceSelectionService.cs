using Iris.Core;

namespace Iris.Services;

public sealed class DeviceSelectionService : IDeviceSelectionService
{
    private DeviceInfo? _selectedDevice;

    public DeviceInfo? SelectedDevice
    {
        get => _selectedDevice;
        set
        {
            if (_selectedDevice == value)
                return;
            _selectedDevice = value;
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? SelectionChanged;
}
