using AOI.Flow.Node;

namespace AOI.Flow.DAG;

public class DagRuntimeNode
{
    public IFlowNode Node { get; }

    public List<DagRuntimeNode> Next { get; } = new();

    public int DependencyCount;

    public DagRuntimeNode(IFlowNode node)
    {
        Node = node;
    }
}