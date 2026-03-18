using AOI.Flow.DAG;
using AOI.Flow.EventBus;
using AOI.Flow.Model;
using System.Collections.Concurrent;

namespace AOI.Flow.Engine;

/// <summary>
/// 工业级多Flow调度器 - 支持独立运行、资源隔离、优先级调度
/// </summary>
public class FlowScheduler : IDisposable
{
    private readonly ConcurrentDictionary<string, FlowInstance> _flows = new();
    private readonly ConcurrentQueue<FlowInstance> _pendingQueue = new();
    private readonly SemaphoreSlim _concurrencyLimiter;
    private readonly int _maxFlowConcurrency;
    private readonly int _maxNodeConcurrencyPerFlow;
    private readonly CancellationTokenSource _schedulerCts = new();
    private readonly Task _dispatchTask;
    private bool _disposed;

    public FlowScheduler(
        int maxFlowConcurrency = 5,
        int maxNodeConcurrencyPerFlow = 10)
    {
        _maxFlowConcurrency = maxFlowConcurrency;
        _maxNodeConcurrencyPerFlow = maxNodeConcurrencyPerFlow;
        _concurrencyLimiter = new SemaphoreSlim(maxFlowConcurrency);
        _dispatchTask = Task.Run(DispatchLoopAsync);
    }

    #region Properties & Query

    public IReadOnlyCollection<FlowInstance> RunningFlows => _flows.Values.Where(f => f.Status == FlowStatus.Running).ToList();
    public IReadOnlyCollection<FlowInstance> AllFlows => _flows.Values.ToList();
    public int PendingCount => _pendingQueue.Count;

    public IEnumerable<FlowInstance> GetFlowsByStatus(FlowStatus status) => _flows.Values.Where(f => f.Status == status);
    public IEnumerable<FlowInstance> GetFlowsByProduct(string productId) => _flows.Values.Where(f => f.ProductId == productId);
    public IEnumerable<FlowInstance> GetFlowsByRecipe(string recipeId) => _flows.Values.Where(f => f.RecipeId == recipeId);

    #endregion

    #region Flow Lifecycle

    public async Task<FlowInstance> ScheduleFlowAsync(
        FlowDefinition definition,
        FlowContext context,
        int priority = 0)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(FlowScheduler));

        var runtimeGraph = DagGraphBuilder.Build(definition);
        var instance = new FlowInstance(definition, context, runtimeGraph)
        {
            Priority = priority,
            EventBus = context.EventBus
        };

        instance.MarkQueued();
        _flows[instance.Id] = instance;
        _pendingQueue.Enqueue(instance);

        return instance;
    }

    private async Task DispatchLoopAsync()
    {
        while (!_schedulerCts.Token.IsCancellationRequested)
        {
            try
            {
                if (_pendingQueue.TryDequeue(out var instance))
                {
                    await _concurrencyLimiter.WaitAsync(_schedulerCts.Token);
                    _ = ExecuteFlowWithIsolationAsync(instance);
                }
                else
                {
                    await Task.Delay(10, _schedulerCts.Token);
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception) { /* Log error */ }
        }
    }

    private async Task ExecuteFlowWithIsolationAsync(FlowInstance instance)
    {
        try
        {
            instance.MarkInit();

            var executor = new DagExecutor(_maxNodeConcurrencyPerFlow);
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                instance.Cts.Token, instance.Context.Token, _schedulerCts.Token);

            instance.MarkRunning();
            await executor.ExecuteAsync(instance.RuntimeGraph, instance.Context);

            if (instance.Status == FlowStatus.Cancelling)
                instance.TransitionTo(FlowStatus.Cancelled);
            else
                instance.MarkCompleted();
        }
        catch (OperationCanceledException)
        {
            if (instance.Status != FlowStatus.Cancelling && instance.Status != FlowStatus.Cancelled)
                instance.TransitionTo(FlowStatus.Cancelled, "Operation cancelled");
        }
        catch (Exception ex)
        {
            instance.MarkFailed(ex);
        }
        finally
        {
            _concurrencyLimiter.Release();
        }
    }

    #endregion

    #region Control Operations

    public bool PauseFlow(string flowId)
    {
        if (_flows.TryGetValue(flowId, out var instance))
            return instance.Pause();
        return false;
    }

    public bool ResumeFlow(string flowId)
    {
        if (_flows.TryGetValue(flowId, out var instance))
            return instance.Resume();
        return false;
    }

    public bool CancelFlow(string flowId, string? reason = null)
    {
        if (_flows.TryGetValue(flowId, out var instance))
            return instance.Cancel(reason);
        return false;
    }

    public void CancelAllFlows(string? reason = null)
    {
        foreach (var flow in _flows.Values.Where(f => !f.IsFinalState))
            flow.Cancel(reason);
    }

    #endregion

    #region Query & Monitoring

    public bool TryGetFlow(string flowId, out FlowInstance? instance) => _flows.TryGetValue(flowId, out instance);

    public FlowInstance? GetFlow(string flowId) => _flows.GetValueOrDefault(flowId);

    public async Task WaitForFlowAsync(string flowId, CancellationToken token = default)
    {
        if (_flows.TryGetValue(flowId, out var instance) && instance.ExecutionTask != null)
            await instance.ExecutionTask.WaitAsync(token);
    }

    public async Task WaitForAllFlowsAsync(CancellationToken token = default)
    {
        var tasks = _flows.Values
            .Where(f => f.ExecutionTask != null)
            .Select(f => f.ExecutionTask!)
            .ToArray();

        if (tasks.Length > 0)
            await Task.WhenAll(tasks).WaitAsync(token);
    }

    public FlowStats[] GetAllStatistics() => _flows.Values.Select(f => f.GetStats()).ToArray();

    public void CleanupCompletedFlows(TimeSpan? maxAge = null)
    {
        var cutoff = DateTime.UtcNow - (maxAge ?? TimeSpan.FromHours(1));
        var toRemove = _flows.Values
            .Where(f => f.IsFinalState && f.EndTime < cutoff)
            .Select(f => f.Id)
            .ToList();

        foreach (var id in toRemove)
            _flows.TryRemove(id, out _);
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _schedulerCts.Cancel();
        CancelAllFlows("Scheduler disposed");

        try { _dispatchTask.Wait(TimeSpan.FromSeconds(5)); }
        catch { /* ignore */ }

        _schedulerCts.Dispose();
        _concurrencyLimiter.Dispose();

        foreach (var flow in _flows.Values)
            flow.Dispose();
    }

    #endregion
}

/// <summary>
/// 调度器统计信息
/// </summary>
public class SchedulerStatistics
{
    public int TotalFlows { get; set; }
    public int RunningFlows { get; set; }
    public int PendingFlows { get; set; }
    public int CompletedFlows { get; set; }
    public int FailedFlows { get; set; }
    public int CancelledFlows { get; set; }
    public int MaxConcurrency { get; set; }
    public int AvailableSlots { get; set; }
}
