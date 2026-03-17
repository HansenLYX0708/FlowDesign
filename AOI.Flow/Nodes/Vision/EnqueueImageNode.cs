using AOI.Flow.Model;
using AOI.Flow.Node;

namespace AOI.Flow.Nodes.Vision;

public class EnqueueImageNode : FlowNodeBase
{
    protected override async Task<NodeResult> OnExecute(
        FlowContext context)
    {
        var img = context.Get<object>("image");

        await context.Pipeline.EnqueueAsync(img);

        return NodeResult.Ok();
    }
}