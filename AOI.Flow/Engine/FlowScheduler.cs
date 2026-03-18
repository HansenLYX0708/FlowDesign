using AOI.Flow.DAG;
using AOI.Flow.Model;
using System.Collections.Concurrent;

namespace AOI.Flow.Engine;

public class FlowScheduler : IDisposable
{
    private readonly ConcurrentDictionary<string, FlowInstance> _runningFlows = new();
    private readonly SemaphoreSlim _globalConcurrencyLimiter;
    private readonly int _maxFlowConcurrency;
    private readonly int _maxNodeConcurrencyPerFlow;
    private bool _disposed;

    public FlowScheduler(
        int maxFlowConcurrency = 5,
        int maxNodeConcurrencyPerFlow = 10)
    {
        _maxFlowConcurrency = maxFlowConcurrency;
        _maxNodeConcurrencyPerFlow = maxNodeConcurrencyPerFlow;
        _globalConcurrencyLimiter = new SemaphoreSlim(maxFlowConcurrency);
    }

    public IReadOnlyCollection<FlowInstance> RunningFlows => _runningFlows.Values.ToList();

    public async Task<FlowInstance> StartFlowAsync(
        FlowDefinition definition,
        FlowContext context)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(FlowScheduler));

        // 等待全局并发许可
        await _globalConcurrencyLimiter.WaitAsync(context.Token);

        try
        {
            // 构建运行时图（每个 Flow 有独立的运行时图）
            var runtimeGraph = DagGraphBuilder.Build(definition);

            // 创建 Flow 实例
            var instance = new FlowInstance(definition, context, runtimeGraph);

            // 添加到运行中的 Flow 集合
            if (!_runningFlows.TryAdd(instance.Id, instance))
            {
                throw new InvalidOperationException("Failed to register flow instance");
            }

            // 启动 Flow 执行
            instance.MarkStarted();
            instance.ExecutionTask = ExecuteFlowAsync(instance);

            return instance;
        }
        catch
        {
            _globalConcurrencyLimiter.Release();
            throw;
        }
    }

    private async Task ExecuteFlowAsync(FlowInstance instance)
    {
        try
        {
            // 创建 DAG 执行器，限制每个 Flow 的节点并发度
            var executor = new DagExecutor(_maxNodeConcurrencyPerFlow);

            // 创建链接的取消令牌
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                instance.CancellationTokenSource.Token,
                instance.Context.Token);

            // 执行 DAG
            await executor.ExecuteAsync(instance.RuntimeGraph, instance.Context);

            instance.MarkCompleted();
        }
        catch (OperationCanceledException)
        {
            instance.MarkCancelled();
        }
        catch (Exception ex)
        {
            instance.MarkFailed(ex);
        }
        finally
        {
            _runningFlows.TryRemove(instance.Id, out _);
            _globalConcurrencyLimiter.Release();
        }
    }

    public bool TryGetFlow(string flowId, out FlowInstance? instance)
    {
        return _runningFlows.TryGetValue(flowId, out instance);
    }

    public bool CancelFlow(string flowId)
    {
        if (_runningFlows.TryGetValue(flowId, out var instance) && instance != null)
        {
            instance.Cancel();
            return true;
        }
        return false;
    }

    public async Task WaitForFlowAsync(string flowId)
    {
        if (_runningFlows.TryGetValue(flowId, out var instance) && instance?.ExecutionTask != null)
        {
            await instance.ExecutionTask;
        }
    }

    public async Task WaitForAllFlowsAsync()
    {
        var tasks = _runningFlows.Values
            .Where(f => f.ExecutionTask != null)
            .Select(f => f.ExecutionTask!)
            .ToArray();

        if (tasks.Length > 0)
        {
            await Task.WhenAll(tasks);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;

        // 取消所有运行中的 Flow
        foreach (var flow in _runningFlows.Values)
        {
            flow.Cancel();
        }

        _globalConcurrencyLimiter.Dispose();
    }
}