namespace AOI.Device.Abstractions.Motion;

public class AxisStatus
{
    public double Position { get; set; }

    public bool IsMoving { get; set; }

    public bool Alarm { get; set; }
}