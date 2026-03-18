using AOI.Flow.DAG;
using AOI.Flow.EventBus;
using AOI.Flow.Model;
using System.Collections.Concurrent;

namespace AOI.Flow.Engine;

#region Flow Status Enum

public enum FlowStatus
{
    Pending = 0,      // 待执行
    Queued,           // 排队中
    Initializing,     // 初始化中
    Running,          // 运行中
    Paused,           // 暂停
    Cancelling,       // 正在取消
    Completed,        // 完成
    Failed,           // 失败
    Cancelled,        // 已取消
    Timeout           // 超时
}

#endregion

#region Flow Instance - Industrial Grade Implementation

public class FlowInstance : IDisposable
{
    #region Identity & Context

    public string Id { get; } = Guid.NewGuid().ToString("N")[..16].ToUpper();
    public FlowDefinition Definition { get; }
    public FlowContext Context { get; }
    public DagRuntimeGraph RuntimeGraph { get; }
    public DateTime CreatedAt { get; } = DateTime.UtcNow;
    public int Priority { get; set; } = 0;
    public string? ParentFlowId { get; set; }
    public List<string> ChildFlowIds { get; } = new();
    public string? ProductId { get; set; }
    public string? RecipeId { get; set; }
    public string TriggerSource { get; set; } = "Manual";
    public Dictionary<string, string> Tags { get; } = new();

    #endregion

    #region State Machine

    private FlowStatus _status = FlowStatus.Pending;
    private readonly object _stateLock = new();

    public FlowStatus Status
    {
        get { lock (_stateLock) return _status; }
    }

    public DateTime? StatusChangedAt { get; private set; }
    public List<StatusHistory> StatusHistory { get; } = new();

    public bool IsFinalState => Status is FlowStatus.Completed or FlowStatus.Failed or FlowStatus.Cancelled or FlowStatus.Timeout;
    public bool CanPause => Status == FlowStatus.Running;
    public bool CanResume => Status == FlowStatus.Paused;
    public bool CanCancel => Status is FlowStatus.Pending or FlowStatus.Queued or FlowStatus.Initializing or FlowStatus.Running or FlowStatus.Paused;

    #endregion

    #region Timing

    public DateTime? StartTime { get; private set; }
    public DateTime? EndTime { get; private set; }
    public TimeSpan ActualDuration { get; private set; }
    public TimeSpan? TotalDuration => EndTime.HasValue ? EndTime.Value - CreatedAt : null;
    private DateTime? _resumeTime;
    private TimeSpan _pausedDuration;

    #endregion

    #region Execution Tracking

    public Task? ExecutionTask { get; internal set; }
    public CancellationTokenSource Cts { get; } = new();
    private readonly ManualResetEventSlim _pauseSignal = new(true);
    public Exception? Error { get; private set; }

    public ConcurrentDictionary<string, NodeState> NodeStates { get; } = new();
    public string? CurrentNodeId { get; private set; }
    public int CompletedNodes => NodeStates.Count(n => n.Value.Status == NodeStatus.Completed);
    public int FailedNodes => NodeStates.Count(n => n.Value.Status == NodeStatus.Failed);
    public int TotalNodes => RuntimeGraph?.Nodes.Count ?? 0;
    public double Progress => TotalNodes > 0 ? (double)CompletedNodes / TotalNodes * 100 : 0;

    #endregion

    #region Events

    public event EventHandler<StateChangedEventArgs>? StateChanged;
    public event EventHandler<NodeEventArgs>? NodeStarted;
    public event EventHandler<NodeEventArgs>? NodeCompleted;
    public event EventHandler<FlowDoneEventArgs>? FlowCompleted;
    public event EventHandler<ProgressEventArgs>? ProgressChanged;
    public IFlowEventBus? EventBus { get; set; }

    #endregion

    public FlowInstance(FlowDefinition def, FlowContext ctx, DagRuntimeGraph graph)
    {
        Definition = def;
        Context = ctx;
        RuntimeGraph = graph;
        foreach (var n in graph.Nodes) NodeStates[n.Node.Id] = new NodeState { Id = n.Node.Id };
        RecordState(FlowStatus.Pending, "Created");
    }

    #region State Transitions

    public bool TransitionTo(FlowStatus newStatus, string? reason = null)
    {
        lock (_stateLock)
        {
            if (!CanTransition(_status, newStatus)) return false;
            var old = _status;
            _status = newStatus;
            StatusChangedAt = DateTime.UtcNow;
            UpdateTiming(old, newStatus);
            RecordState(newStatus, reason);
            FireStateChanged(old, newStatus);
            return true;
        }
    }

    private static bool CanTransition(FlowStatus from, FlowStatus to) => (from, to) switch
    {
        (FlowStatus.Pending, FlowStatus.Queued or FlowStatus.Initializing or FlowStatus.Cancelled) => true,
        (FlowStatus.Queued, FlowStatus.Initializing or FlowStatus.Cancelled) => true,
        (FlowStatus.Initializing, FlowStatus.Running or FlowStatus.Failed or FlowStatus.Cancelled) => true,
        (FlowStatus.Running, FlowStatus.Paused or FlowStatus.Cancelling or FlowStatus.Completed or FlowStatus.Failed or FlowStatus.Timeout) => true,
        (FlowStatus.Paused, FlowStatus.Running or FlowStatus.Cancelling) => true,
        (FlowStatus.Cancelling, FlowStatus.Cancelled or FlowStatus.Failed) => true,
        _ => false
    };

    private void UpdateTiming(FlowStatus oldSt, FlowStatus newSt)
    {
        switch (newSt)
        {
            case FlowStatus.Running:
                if (!StartTime.HasValue) { StartTime = DateTime.UtcNow; _resumeTime = StartTime; }
                else if (oldSt == FlowStatus.Paused) { _resumeTime = DateTime.UtcNow; }
                _pauseSignal.Set();
                break;
            case FlowStatus.Paused:
                if (_resumeTime.HasValue) _pausedDuration += DateTime.UtcNow - _resumeTime.Value;
                _pauseSignal.Reset();
                break;
            case FlowStatus.Completed or FlowStatus.Failed or FlowStatus.Cancelled or FlowStatus.Timeout:
                EndTime = DateTime.UtcNow;
                if (StartTime.HasValue) ActualDuration = EndTime.Value - StartTime.Value - _pausedDuration;
                _pauseSignal.Set();
                break;
        }
    }

    private void RecordState(FlowStatus st, string? reason) =>
        StatusHistory.Add(new StatusHistory { Time = DateTime.UtcNow, Status = st, Reason = reason });

    private void FireStateChanged(FlowStatus oldSt, FlowStatus newSt)
    {
        StateChanged?.Invoke(this, new StateChangedEventArgs { OldStatus = oldSt, NewStatus = newSt });
        EventBus?.PublishAsync(new FlowStatusChangedEvent
        {
            Source = Id, OldStatus = oldSt, NewStatus = newSt, ProductId = ProductId, RecipeId = RecipeId
        }, Cts.Token);
    }

    #endregion

    #region Node Tracking

    public void OnNodeStart(string nodeId)
    {
        CurrentNodeId = nodeId;
        if (NodeStates.TryGetValue(nodeId, out var s)) { s.Status = NodeStatus.Running; s.StartTime = DateTime.UtcNow; s.Attempts++; }
        NodeStarted?.Invoke(this, new NodeEventArgs { NodeId = nodeId });
    }

    public void OnNodeDone(string nodeId, bool ok, string? err = null, double ms = 0)
    {
        if (NodeStates.TryGetValue(nodeId, out var s))
        {
            s.Status = ok ? NodeStatus.Completed : NodeStatus.Failed;
            s.EndTime = DateTime.UtcNow;
            s.DurationMs = ms;
            s.Error = err;
        }
        NodeCompleted?.Invoke(this, new NodeEventArgs { NodeId = nodeId, Success = ok, Error = err, DurationMs = ms });
        ProgressChanged?.Invoke(this, new ProgressEventArgs { Percent = Progress, CurrentNode = nodeId });
    }

    public void WaitIfPaused() => _pauseSignal.Wait(Cts.Token);
    public void CheckCancel() => Cts.Token.ThrowIfCancellationRequested();

    #endregion

    #region Control Operations

    public bool Pause(string? r = null) => TransitionTo(FlowStatus.Paused, r ?? "Paused");
    public bool Resume(string? r = null) => TransitionTo(FlowStatus.Running, r ?? "Resumed");

    public bool Cancel(string? r = null)
    {
        if (!CanCancel) return false;
        Cts.Cancel();
        TransitionTo(Status == FlowStatus.Running || Status == FlowStatus.Paused ? FlowStatus.Cancelling : FlowStatus.Cancelled, r ?? "Cancelled");
        _pauseSignal.Set();
        return true;
    }

    public void MarkQueued() => TransitionTo(FlowStatus.Queued);
    public void MarkInit() => TransitionTo(FlowStatus.Initializing);
    public void MarkRunning() => TransitionTo(FlowStatus.Running);

    public void MarkCompleted(string? r = null)
    {
        if (TransitionTo(FlowStatus.Completed, r ?? "Completed"))
            FlowCompleted?.Invoke(this, new FlowDoneEventArgs { Success = true });
    }

    public void MarkFailed(Exception ex, string? d = null)
    {
        Error = ex;
        if (TransitionTo(FlowStatus.Failed, ex.Message))
            FlowCompleted?.Invoke(this, new FlowDoneEventArgs { Success = false, Error = ex, Details = d });
    }

    public void MarkTimeout(TimeSpan t) => TransitionTo(FlowStatus.Timeout, $"Timeout {t}");

    #endregion

    #region Statistics

    public FlowStats GetStats() => new()
    {
        Id = Id,
        Status = Status,
        Created = CreatedAt,
        Started = StartTime,
        Ended = EndTime,
        Total = TotalDuration,
        Actual = ActualDuration,
        TotalNodes = TotalNodes,
        Done = CompletedNodes,
        Failed = FailedNodes,
        Progress = Progress,
        Current = CurrentNodeId
    };

    public FlowSnapshot GetSnapshot() => new()
    {
        Id = Id,
        Status = Status,
        Product = ProductId,
        Recipe = RecipeId,
        Progress = Progress,
        Current = CurrentNodeId,
        CanPause = CanPause,
        CanCancel = CanCancel
    };

    #endregion

    public void Dispose()
    {
        _pauseSignal?.Dispose();
        Cts?.Dispose();
        ExecutionTask?.Dispose();
    }
}

#endregion

#region Supporting Types

public enum NodeStatus { Pending, Running, Completed, Failed, Cancelled }

public class NodeState
{
    public string Id { get; set; } = "";
    public NodeStatus Status { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public double DurationMs { get; set; }
    public int Attempts { get; set; }
    public string? Error { get; set; }
}

public class StatusHistory
{
    public DateTime Time { get; set; }
    public FlowStatus Status { get; set; }
    public string? Reason { get; set; }
}

public class StateChangedEventArgs : EventArgs
{
    public FlowStatus OldStatus { get; set; }
    public FlowStatus NewStatus { get; set; }
}

public class NodeEventArgs : EventArgs
{
    public string NodeId { get; set; } = "";
    public bool Success { get; set; }
    public string? Error { get; set; }
    public double DurationMs { get; set; }
}

public class FlowDoneEventArgs : EventArgs
{
    public bool Success { get; set; }
    public Exception? Error { get; set; }
    public string? Details { get; set; }
}

public class ProgressEventArgs : EventArgs
{
    public double Percent { get; set; }
    public string? CurrentNode { get; set; }
}

public class FlowStats
{
    public string Id { get; set; } = "";
    public FlowStatus Status { get; set; }
    public DateTime Created { get; set; }
    public DateTime? Started { get; set; }
    public DateTime? Ended { get; set; }
    public TimeSpan? Total { get; set; }
    public TimeSpan Actual { get; set; }
    public int TotalNodes { get; set; }
    public int Done { get; set; }
    public int Failed { get; set; }
    public double Progress { get; set; }
    public string? Current { get; set; }
}

public class FlowSnapshot
{
    public string Id { get; set; } = "";
    public FlowStatus Status { get; set; }
    public string? Product { get; set; }
    public string? Recipe { get; set; }
    public double Progress { get; set; }
    public string? Current { get; set; }
    public bool CanPause { get; set; }
    public bool CanCancel { get; set; }
}

#endregion
