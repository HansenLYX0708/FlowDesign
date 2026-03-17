using AOI.Device.Abstractions.Base;

namespace AOI.Device.Abstractions.IO;

public interface IIOController : IDevice
{
    bool ReadInput(int channel);

    void WriteOutput(int channel, bool value);

    event Action<IOState>? InputChanged;
}