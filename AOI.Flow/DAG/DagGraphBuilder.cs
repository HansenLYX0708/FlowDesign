using AOI.Flow.Model;
using AOI.Flow.Node;

namespace AOI.Flow.DAG;

public static class DagGraphBuilder
{
    public static DagRuntimeGraph Build(FlowDefinition definition)
    {
        if (definition.Nodes.Count == 0)
            throw new Exception("Flow has no nodes");

        var graph = new DagRuntimeGraph();

        var map = definition.Nodes.ToDictionary(
            n => n.Id,
            n => new DagRuntimeNode(n));

        graph.Nodes.AddRange(map.Values);

        foreach (var edge in definition.Edges)
        {
            if (!map.ContainsKey(edge.From))
                throw new Exception($"Node not found {edge.From}");

            if (!map.ContainsKey(edge.To))
                throw new Exception($"Node not found {edge.To}");

            var from = map[edge.From];
            var to = map[edge.To];

            from.Next.Add(to);

            to.DependencyCount++;
        }

        DetectCycle(graph);

        return graph;
    }

    private static void DetectCycle(DagRuntimeGraph graph)
    {
        var dep = graph.Nodes.ToDictionary(
            n => n,
            n => n.DependencyCount);

        var queue = new Queue<DagRuntimeNode>(
            graph.Nodes.Where(n => n.DependencyCount == 0));

        int visited = 0;

        while (queue.Count > 0)
        {
            var node = queue.Dequeue();

            visited++;

            foreach (var next in node.Next)
            {
                dep[next]--;

                if (dep[next] == 0)
                    queue.Enqueue(next);
            }
        }

        if (visited != graph.Nodes.Count)
            throw new Exception("DAG contains cycle");
    }
}