using AOI.Device.Abstractions.Base;

namespace AOI.Device.Abstractions.Laser;

public interface ILaserRangeFinder : IDevice
{
    Task<double> MeasureAsync();
}