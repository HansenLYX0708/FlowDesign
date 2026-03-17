using AOI.Device.Abstractions.Base;
using System.Collections.Concurrent;

namespace AOI.Device.Registry;

public class DeviceRegistry
{
    private readonly ConcurrentDictionary<string, IDevice> _devices = new();

    public void Register(IDevice device)
    {
        _devices[device.Id] = device;
    }

    public IEnumerable<IDevice> GetAll()
    {
        return _devices.Values;
    }

    public T? Get<T>() where T : class, IDevice
    {
        return _devices.Values.OfType<T>().FirstOrDefault();
    }

    public IDevice? GetById(string id)
    {
        _devices.TryGetValue(id, out var device);

        return device;
    }
}