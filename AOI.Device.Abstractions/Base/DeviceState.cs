namespace AOI.Device.Abstractions.Base;

public enum DeviceState
{
    Unknown,
    Disconnected,
    Connected,
    Initializing,
    Ready,
    Busy,
    Error
}