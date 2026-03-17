using AOI.Core.Logging;
using AOI.Device.Abstractions.Base;
using AOI.Device.Abstractions.Motion;

namespace AOI.Device.Plugins.Simulation;

public class MockAxis : DeviceBase, IAxis
{
    private readonly AxisStatus _status = new();

    public async Task MoveToAsync(double position)
    {
        Logger.Info($"MockAxis move to {position}");

        _status.IsMoving = true;

        await Task.Delay(500);

        _status.Position = position;

        _status.IsMoving = false;
    }

    public async Task HomeAsync()
    {
        await MoveToAsync(0);
    }

    public async Task StopAsync()
    {
        _status.IsMoving = false;

        await Task.CompletedTask;
    }

    public AxisStatus GetStatus()
    {
        return _status;
    }
}