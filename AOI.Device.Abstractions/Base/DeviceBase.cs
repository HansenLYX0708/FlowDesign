using AOI.Core.Logging;

namespace AOI.Device.Abstractions.Base;

public abstract class DeviceBase : IDevice
{
    public string Id { get; protected set; } = Guid.NewGuid().ToString();

    public DeviceInfo Info { get; protected set; } = new();

    public DeviceState State { get; protected set; } = DeviceState.Disconnected;

    public virtual async Task ConnectAsync()
    {
        Logger.Info($"{Id} connecting");

        State = DeviceState.Connected;

        await Task.CompletedTask;
    }

    public virtual async Task DisconnectAsync()
    {
        Logger.Info($"{Id} disconnect");

        State = DeviceState.Disconnected;

        await Task.CompletedTask;
    }

    public virtual async Task InitializeAsync()
    {
        Logger.Info($"{Id} initialize");

        State = DeviceState.Initializing;

        await Task.Delay(100);

        State = DeviceState.Ready;
    }

    public virtual async Task ResetAsync()
    {
        Logger.Warn($"{Id} reset");

        await InitializeAsync();
    }
}