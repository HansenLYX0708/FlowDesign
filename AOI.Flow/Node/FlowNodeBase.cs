using AOI.Core.Logging;
using AOI.Flow.Model;

namespace AOI.Flow.Node;

public abstract class FlowNodeBase : IFlowNode
{
    public string Id { get; init; } = Guid.NewGuid().ToString();

    public async Task<NodeResult> ExecuteAsync(FlowContext context)
    {
        try
        {
            Logger.Info($"Node Start {Id}");

            return await OnExecute(context);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "FlowNode Error");

            return NodeResult.Fail(ex.Message);
        }
    }

    protected abstract Task<NodeResult> OnExecute(
        FlowContext context);
}