using AOI.Core.Logging;
using AOI.Device.Abstractions.Base;
using AOI.Device.Abstractions.Camera;

namespace AOI.Device.Plugins.Simulation;

public class MockCamera : DeviceBase, ICamera
{
    private bool _grabbing;

    public event Action<CameraFrame>? FrameReceived;

    public async Task StartGrabbingAsync()
    {
        _grabbing = true;

        _ = Task.Run(GenerateLoop);

        await Task.CompletedTask;
    }

    public async Task StopGrabbingAsync()
    {
        _grabbing = false;

        await Task.CompletedTask;
    }

    public async Task<CameraFrame> GrabAsync()
    {
        var frame = GenerateFrame();

        FrameReceived?.Invoke(frame);

        return await Task.FromResult(frame);
    }

    private async Task GenerateLoop()
    {
        while (_grabbing)
        {
            var frame = GenerateFrame();

            FrameReceived?.Invoke(frame);

            await Task.Delay(100);
        }
    }

    private CameraFrame GenerateFrame()
    {
        return new CameraFrame
        {
            Width = 640,
            Height = 480,
            Timestamp = DateTime.Now,
            Data = new byte[640 * 480]
        };
    }
}