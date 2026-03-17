using AOI.Device.Abstractions.Base;

namespace AOI.Device.Abstractions.Light;

public interface ILightController : IDevice
{
    void SetIntensity(int channel, int value);

    void TurnOn(int channel);

    void TurnOff(int channel);
}