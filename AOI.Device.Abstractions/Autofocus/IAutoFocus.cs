using AOI.Device.Abstractions.Base;

namespace AOI.Device.Abstractions.Autofocus;

public interface IAutoFocus : IDevice
{
    Task<double> FocusAsync();
}