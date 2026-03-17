namespace AOI.Device.Abstractions.Events;

public class DeviceEvent
{
    public string DeviceId { get; set; } = "";

    public string Message { get; set; } = "";

    public DateTime Time { get; set; } = DateTime.Now;
}