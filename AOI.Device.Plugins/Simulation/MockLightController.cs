using AOI.Core.Logging;
using AOI.Device.Abstractions.Base;
using AOI.Device.Abstractions.Light;

namespace AOI.Device.Plugins.Simulation;

public class MockLightController : DeviceBase, ILightController
{
    public void SetIntensity(int channel, int value)
    {
        Logger.Info($"MockLight channel {channel} intensity {value}");
    }

    public void TurnOn(int channel)
    {
        Logger.Info($"MockLight {channel} ON");
    }

    public void TurnOff(int channel)
    {
        Logger.Info($"MockLight {channel} OFF");
    }
}