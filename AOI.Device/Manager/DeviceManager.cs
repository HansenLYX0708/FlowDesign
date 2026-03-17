using AOI.Core.Logging;
using AOI.Device.Abstractions.Base;
using AOI.Device.Loader;
using AOI.Device.Registry;

namespace AOI.Device.Manager;

public class DeviceManager
{
    private readonly DeviceRegistry _registry = new();

    private readonly DevicePluginLoader _loader = new();

    public IReadOnlyCollection<IDevice> Devices
        => _registry.GetAll().ToList();

    public async Task LoadPluginsAsync(string pluginFolder)
    {
        Logger.Info("Loading device plugins");

        var devices = _loader.LoadDevices(pluginFolder);

        foreach (var device in devices)
        {
            _registry.Register(device);

            Logger.Info($"Device loaded: {device.Id}");
        }

        await Task.CompletedTask;
    }

    public T? Get<T>() where T : class, IDevice
    {
        return _registry.Get<T>();
    }

    public IEnumerable<IDevice> GetAll()
    {
        return _registry.GetAll();
    }
}