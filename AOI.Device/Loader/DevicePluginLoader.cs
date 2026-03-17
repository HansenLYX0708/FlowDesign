using AOI.Device.Abstractions.Base;
using AOI.Infrastructure.Plugin;

namespace AOI.Device.Loader;

public class DevicePluginLoader
{
    public IEnumerable<IDevice> LoadDevices(string pluginFolder)
    {
        return PluginLoader.LoadPlugins<IDevice>(pluginFolder);
    }
}