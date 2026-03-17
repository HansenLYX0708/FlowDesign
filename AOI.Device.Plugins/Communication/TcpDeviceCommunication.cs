using AOI.Device.Abstractions.Base;
using AOI.Device.Abstractions.Communication;
using AOI.Infrastructure.Communication;

namespace AOI.Device.Plugins.Communication;

public class TcpDeviceCommunication : DeviceBase, IDeviceCommunication
{
    private readonly TcpClientEx _client = new();

    public event Action<string>? MessageReceived;

    public async Task SendAsync(string message)
    {
        await _client.SendAsync(message);
    }
}