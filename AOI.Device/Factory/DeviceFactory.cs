using AOI.Device.Abstractions.Base;

namespace AOI.Device.Factory;

public static class DeviceFactory
{
    public static IDevice Create(Type type)
    {
        return (IDevice)Activator.CreateInstance(type)!;
    }

    public static T Create<T>() where T : IDevice
    {
        return (T)Activator.CreateInstance(typeof(T))!;
    }
}