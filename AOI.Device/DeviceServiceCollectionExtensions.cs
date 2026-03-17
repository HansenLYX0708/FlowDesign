using AOI.Device.Lifecycle;
using AOI.Device.Manager;
using AOI.Device.Monitoring;
using AOI.Device.Registry;
using Microsoft.Extensions.DependencyInjection;

namespace AOI.Device;

public static class DeviceServiceCollectionExtensions
{
    public static IServiceCollection AddDeviceSystem(
        this IServiceCollection services)
    {
        services.AddSingleton<DeviceRegistry>();

        services.AddSingleton<DeviceManager>();

        services.AddSingleton<DeviceLifecycleService>();

        services.AddSingleton<DeviceMonitor>();

        return services;
    }
}