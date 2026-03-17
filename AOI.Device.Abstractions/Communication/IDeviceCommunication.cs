using AOI.Device.Abstractions.Base;

namespace AOI.Device.Abstractions.Communication;

public interface IDeviceCommunication : IDevice
{
    Task SendAsync(string message);

    event Action<string>? MessageReceived;
}