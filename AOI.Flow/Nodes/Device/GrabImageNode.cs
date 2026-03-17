using AOI.Device.Abstractions.Camera;
using AOI.Flow.Model;
using AOI.Flow.Node;

namespace AOI.Flow.Nodes.Device;

public class GrabImageNode : FlowNodeBase
{
    protected override async Task<NodeResult> OnExecute(
        FlowContext context)
    {
        var cam = context.DeviceManager.Get<ICamera>();

        var img = await cam.GrabAsync();

        context.Set("image", img);

        return NodeResult.Ok();
    }
}