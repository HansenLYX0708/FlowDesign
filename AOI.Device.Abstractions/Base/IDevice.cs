namespace AOI.Device.Abstractions.Base;

public interface IDevice
{
    string Id { get; }

    DeviceInfo Info { get; }

    DeviceState State { get; }

    Task ConnectAsync();

    Task DisconnectAsync();

    Task InitializeAsync();

    Task ResetAsync();
}