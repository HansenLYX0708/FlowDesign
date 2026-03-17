using AOI.Core.Logging;
using AOI.Device.Registry;

namespace AOI.Device.Monitoring;

public class DeviceMonitor
{
    private readonly DeviceRegistry _registry;

    public DeviceMonitor(DeviceRegistry registry)
    {
        _registry = registry;
    }

    public void Start()
    {
        Task.Run(async () =>
        {
            while (true)
            {
                foreach (var device in _registry.GetAll())
                {
                    Logger.Debug($"Device {device.Id} State: {device.State}");
                }

                await Task.Delay(2000);
            }
        });
    }
}