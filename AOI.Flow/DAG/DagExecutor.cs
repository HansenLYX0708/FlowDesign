using AOI.Flow.Model;
using System.Threading.Channels;

namespace AOI.Flow.DAG;

public class DagExecutor
{
    private readonly int _maxConcurrency;

    public DagExecutor(int maxConcurrency = 10)
    {
        _maxConcurrency = maxConcurrency > 0 ? maxConcurrency : Environment.ProcessorCount;
    }

    public async Task ExecuteAsync(
        DagRuntimeGraph graph,
        FlowContext context)
    {
        // 创建取消令牌源，链接外部令牌
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(context.Token);
        var token = cts.Token;

        // 初始化节点状态
        var nodeStates = graph.Nodes.ToDictionary(
            n => n.Node.Id,
            n => new NodeState(n));

        var readyChannel = Channel.CreateUnbounded<DagRuntimeNode>(
            new UnboundedChannelOptions { SingleReader = false, SingleWriter = true });

        var completedChannel = Channel.CreateUnbounded<NodeResultMessage>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

        int remainingNodes = graph.Nodes.Count;
        Exception? capturedException = null;
        var completionLock = new object();

        // 将初始就绪节点（入度为0）加入队列
        foreach (var node in graph.Nodes.Where(n => n.InitialDependencyCount == 0))
        {
            readyChannel.Writer.TryWrite(node);
        }
        readyChannel.Writer.Complete();

        // 启动完成处理器（单消费者）
        var completionTask = Task.Run(async () =>
        {
            await foreach (var msg in completedChannel.Reader.ReadAllAsync(token))
            {
                var state = nodeStates[msg.NodeId];
                state.IsCompleted = true;
                state.Success = msg.Success;
                state.ErrorMessage = msg.ErrorMessage;

                if (!msg.Success && capturedException == null)
                {
                    capturedException = new Exception(
                        $"Node '{msg.NodeId}' failed: {msg.ErrorMessage}");
                    cts.Cancel();
                    return;
                }

                // 通知后继节点：依赖已完成
                foreach (var next in state.RuntimeNode.Next)
                {
                    var nextState = nodeStates[next.Node.Id];
                    if (Interlocked.Decrement(ref nextState.RemainingDeps) == 0)
                    {
                        // 所有依赖都完成了
                    }
                }

                if (Interlocked.Decrement(ref remainingNodes) == 0)
                {
                    completedChannel.Writer.Complete();
                }
            }
        }, token);

        // 启动工作线程（多消费者），使用信号量控制并发度
        using var semaphore = new SemaphoreSlim(_maxConcurrency);
        var workers = new List<Task>();

        for (int i = 0; i < _maxConcurrency; i++)
        {
            workers.Add(Task.Run(async () =>
            {
                await foreach (var node in readyChannel.Reader.ReadAllAsync(token))
                {
                    await semaphore.WaitAsync(token);
                    try
                    {
                        if (token.IsCancellationRequested)
                            break;

                        var state = nodeStates[node.Node.Id];
                        if (state.IsStarted)
                            continue; // 防止重复执行

                        state.IsStarted = true;

                        try
                        {
                            var result = await node.Node.ExecuteAsync(context);

                            await completedChannel.Writer.WriteAsync(
                                new NodeResultMessage(node.Node.Id, result.Success, result.ErrorMessage), token);
                        }
                        catch (OperationCanceledException)
                        {
                            throw;
                        }
                        catch (Exception ex)
                        {
                            await completedChannel.Writer.WriteAsync(
                                new NodeResultMessage(node.Node.Id, false, ex.Message), token);
                        }
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }
            }, token));
        }

        // 启动调度器：监控依赖变化，将新就绪节点加入处理
        var schedulerTask = Task.Run(async () =>
        {
            var processed = new HashSet<string>();
            while (remainingNodes > 0 && !token.IsCancellationRequested)
            {
                bool anyNewReady = false;
                foreach (var kvp in nodeStates)
                {
                    if (processed.Contains(kvp.Key))
                        continue;

                    var state = kvp.Value;
                    if (state.RemainingDeps == 0 && !state.IsStarted)
                    {
                        // 此节点已就绪，创建独立任务执行
                        processed.Add(kvp.Key);
                        anyNewReady = true;

                        _ = Task.Run(async () =>
                        {
                            await semaphore.WaitAsync(token);
                            try
                            {
                                if (token.IsCancellationRequested || state.IsStarted)
                                    return;

                                state.IsStarted = true;

                                try
                                {
                                    var result = await state.RuntimeNode.Node.ExecuteAsync(context);
                                    await completedChannel.Writer.WriteAsync(
                                        new NodeResultMessage(kvp.Key, result.Success, result.ErrorMessage), token);
                                }
                                catch (OperationCanceledException)
                                {
                                    throw;
                                }
                                catch (Exception ex)
                                {
                                    await completedChannel.Writer.WriteAsync(
                                        new NodeResultMessage(kvp.Key, false, ex.Message), token);
                                }
                            }
                            finally
                            {
                                semaphore.Release();
                            }
                        }, token);
                    }
                }

                if (!anyNewReady)
                {
                    // 检查是否死锁（没有就绪节点但还有未完成节点）
                    bool anyIncomplete = nodeStates.Values.Any(s => !s.IsCompleted);
                    if (!anyIncomplete || remainingNodes <= 0)
                        break;
                }

                await Task.Delay(5, token);
            }

            // 所有节点处理完毕，关闭完成通道
            if (!completedChannel.Writer.TryComplete())
            {
                // 如果已经有异常导致关闭，忽略
            }
        }, token);

        try
        {
            // 等待所有工作完成
            await Task.WhenAll(workers);
            await schedulerTask;
            await completionTask;
        }
        catch (OperationCanceledException)
        {
            // 正常取消
        }

        // 确保通道关闭
        readyChannel.Writer.TryComplete();
        completedChannel.Writer.TryComplete();

        // 抛出捕获的异常
        if (capturedException != null)
        {
            throw new AggregateException("DAG execution failed", capturedException);
        }

        // 验证所有节点完成
        var incompleteNodes = nodeStates.Where(s => !s.Value.IsCompleted).ToList();
        if (incompleteNodes.Any())
        {
            throw new Exception(
                $"DAG execution incomplete. Remaining: {incompleteNodes.Count} nodes: " +
                string.Join(", ", incompleteNodes.Select(n => n.Key)));
        }
    }

    private class NodeState
    {
        public DagRuntimeNode RuntimeNode { get; }
        public int RemainingDeps;
        public bool IsStarted;
        public bool IsCompleted;
        public bool Success;
        public string? ErrorMessage;

        public NodeState(DagRuntimeNode node)
        {
            RuntimeNode = node;
            RemainingDeps = node.InitialDependencyCount;
        }
    }

    private record NodeResultMessage(string NodeId, bool Success, string? ErrorMessage);
}