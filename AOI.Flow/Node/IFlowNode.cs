using AOI.Flow.Model;

namespace AOI.Flow.Node;

public interface IFlowNode
{
    string Id { get; }

    Task<NodeResult> ExecuteAsync(FlowContext context);
}