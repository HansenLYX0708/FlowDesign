using AOI.Device.Abstractions.Base;
using AOI.Device.Abstractions.IO;

namespace AOI.Device.Plugins.Simulation;

public class MockIOController : DeviceBase, IIOController
{
    private readonly Dictionary<int, bool> _outputs = new();

    public event Action<IOState>? InputChanged;

    public bool ReadInput(int channel)
    {
        return false;
    }

    public void WriteOutput(int channel, bool value)
    {
        _outputs[channel] = value;
    }
}