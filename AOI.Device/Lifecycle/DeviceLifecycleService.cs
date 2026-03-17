using AOI.Core.Logging;
using AOI.Device.Abstractions.Base;
using AOI.Device.Registry;

namespace AOI.Device.Lifecycle;

public class DeviceLifecycleService
{
    private readonly DeviceRegistry _registry;

    public DeviceLifecycleService(DeviceRegistry registry)
    {
        _registry = registry;
    }

    public async Task ConnectAllAsync()
    {
        foreach (var device in _registry.GetAll())
        {
            Logger.Info($"Connecting {device.Id}");

            await device.ConnectAsync();
        }
    }

    public async Task InitializeAllAsync()
    {
        foreach (var device in _registry.GetAll())
        {
            Logger.Info($"Initialize {device.Id}");

            await device.InitializeAsync();
        }
    }

    public async Task ShutdownAllAsync()
    {
        foreach (var device in _registry.GetAll())
        {
            await device.DisconnectAsync();
        }
    }
}