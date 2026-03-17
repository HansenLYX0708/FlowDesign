using AOI.Device.Abstractions.Autofocus;
using AOI.Device.Abstractions.Base;

namespace AOI.Device.Plugins.Simulation;

public class MockAutoFocus : DeviceBase, IAutoFocus
{
    public async Task<double> FocusAsync()
    {
        await Task.Delay(300);

        return Random.Shared.NextDouble();
    }
}