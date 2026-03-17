using AOI.Flow.Model;
using System.Collections.Concurrent;

namespace AOI.Flow.DAG;

public class DagExecutor
{
    public async Task ExecuteAsync(
        DagRuntimeGraph graph,
        FlowContext context)
    {
        var readyQueue = new ConcurrentQueue<DagRuntimeNode>();

        foreach (var node in graph.Nodes)
        {
            if (node.DependencyCount == 0)
                readyQueue.Enqueue(node);
        }

        var runningTasks = new List<Task>();

        while (readyQueue.TryDequeue(out var node))
        {
            var task = ExecuteNode(node, readyQueue, context);

            runningTasks.Add(task);
        }

        await Task.WhenAll(runningTasks);
    }

    private async Task ExecuteNode(
        DagRuntimeNode node,
        ConcurrentQueue<DagRuntimeNode> queue,
        FlowContext context)
    {
        var result = await node.Node.ExecuteAsync(context);

        if (!result.Success)
            throw new Exception($"Node failed: {node.Node.Id}");

        foreach (var next in node.Next)
        {
            if (Interlocked.Decrement(ref next.DependencyCount) == 0)
            {
                queue.Enqueue(next);

                _ = ExecuteNode(next, queue, context);
            }
        }
    }
}