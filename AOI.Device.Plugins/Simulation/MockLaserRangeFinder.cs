using AOI.Device.Abstractions.Base;
using AOI.Device.Abstractions.Laser;

namespace AOI.Device.Plugins.Simulation;

public class MockLaserRangeFinder : DeviceBase, ILaserRangeFinder
{
    public async Task<double> MeasureAsync()
    {
        await Task.Delay(100);

        return Random.Shared.NextDouble() * 10;
    }
}