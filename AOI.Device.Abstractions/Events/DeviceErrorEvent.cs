namespace AOI.Device.Abstractions.Events;

public class DeviceErrorEvent
{
    public string DeviceId { get; set; } = "";

    public string Error { get; set; } = "";

    public DateTime Time { get; set; } = DateTime.Now;
}