using AOI.Device.Abstractions.Base;

namespace AOI.Device.Abstractions.Motion;

public interface IAxis : IDevice
{
    Task MoveToAsync(double position);

    Task HomeAsync();

    Task StopAsync();

    AxisStatus GetStatus();
}