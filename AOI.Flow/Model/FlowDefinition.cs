using AOI.Flow.Node;

namespace AOI.Flow.Model;

public class FlowDefinition
{
    public string Name { get; set; } = "";

    public List<IFlowNode> Nodes { get; } = new();

    public List<(string From, string To)> Edges { get; }
        = new();
}