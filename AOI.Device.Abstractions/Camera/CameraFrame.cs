namespace AOI.Device.Abstractions.Camera;

public class CameraFrame
{
    public byte[] Data { get; set; } = [];

    public int Width { get; set; }

    public int Height { get; set; }

    public DateTime Timestamp { get; set; }
}