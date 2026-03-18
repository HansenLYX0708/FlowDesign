using AOI.Flow.Node;

namespace AOI.Flow.DAG;

public class DagRuntimeNode
{
    public IFlowNode Node { get; }

    public List<DagRuntimeNode> Next { get; } = new();

    // 初始依赖计数（不可变，用于复用 FlowDefinition）
    public int InitialDependencyCount { get; set; }

    // 运行时依赖计数（执行时使用）
    public int RemainingDependencies;

    public DagRuntimeNode(IFlowNode node)
    {
        Node = node;
    }
}