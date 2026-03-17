using AOI.Device.Manager;
using AOI.Flow.DAG;
using AOI.Flow.Model;

namespace AOI.Flow.Engine;

public class FlowEngine
{
    private readonly DeviceManager _deviceManager;

    public FlowEngine(DeviceManager deviceManager)
    {
        _deviceManager = deviceManager;
    }

    public async Task StartFlowAsync(
        FlowDefinition definition)
    {
        var context = new FlowContext(
            _deviceManager,
            null!,
            CancellationToken.None);

        var runtimeGraph =
            DagGraphBuilder.Build(definition);

        var executor = new DagExecutor();

        await executor.ExecuteAsync(runtimeGraph, context);
    }
}