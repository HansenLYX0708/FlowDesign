using AOI.Device.Abstractions.Base;

namespace AOI.Device.Abstractions.Camera;

public interface ICamera : IDevice
{
    Task StartGrabbingAsync();

    Task StopGrabbingAsync();

    Task<CameraFrame> GrabAsync();

    event Action<CameraFrame>? FrameReceived;
}