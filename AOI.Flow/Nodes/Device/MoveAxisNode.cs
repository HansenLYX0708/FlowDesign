using AOI.Device.Abstractions.Motion;
using AOI.Flow.Model;
using AOI.Flow.Node;

namespace AOI.Flow.Nodes.Device;

public class MoveAxisNode : FlowNodeBase
{
    public double Position { get; set; }

    protected override async Task<NodeResult> OnExecute(
        FlowContext context)
    {
        var axis = context.DeviceManager.Get<IAxis>();

        await axis.MoveToAsync(Position);

        return NodeResult.Ok();
    }
}