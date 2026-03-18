using AOI.Flow.Model;

namespace AOI.Flow.Node;

/// <summary>
/// 节点执行状态 - 工业级状态机
/// </summary>
public enum NodeExecutionStatus
{
    Ready,      // 就绪，等待执行
    Running,    // 正在执行
    Success,    // 执行成功
    Failed,     // 执行失败
    Retrying,   // 重试中
    Skipped     // 被跳过
}

/// <summary>
/// 节点状态转换规则
/// </summary>
public static class NodeStateTransitions
{
    public static bool CanTransition(NodeExecutionStatus from, NodeExecutionStatus to) => (from, to) switch
    {
        // 从 Ready 可以转移到 Running 或 Skipped
        (NodeExecutionStatus.Ready, NodeExecutionStatus.Running) => true,
        (NodeExecutionStatus.Ready, NodeExecutionStatus.Skipped) => true,

        // 从 Running 可以转移到 Success、Failed 或 Retrying
        (NodeExecutionStatus.Running, NodeExecutionStatus.Success) => true,
        (NodeExecutionStatus.Running, NodeExecutionStatus.Failed) => true,
        (NodeExecutionStatus.Running, NodeExecutionStatus.Retrying) => true,

        // 从 Retrying 可以转移到 Running
        (NodeExecutionStatus.Retrying, NodeExecutionStatus.Running) => true,

        // 从 Failed 可以转移到 Retrying（如果还有重试次数）
        (NodeExecutionStatus.Failed, NodeExecutionStatus.Retrying) => true,

        // 终态无法再转移
        (NodeExecutionStatus.Success, _) => false,
        (NodeExecutionStatus.Skipped, _) => false,

        _ => false
    };
}

/// <summary>
/// 重试策略
/// </summary>
public class RetryPolicy
{
    /// <summary>
    /// 最大重试次数
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// 重试间隔（毫秒）
    /// </summary>
    public int RetryIntervalMs { get; set; } = 1000;

    /// <summary>
    /// 是否使用指数退避
    /// </summary>
    public bool UseExponentialBackoff { get; set; } = true;

    /// <summary>
    /// 指数退避倍数
    /// </summary>
    public double BackoffMultiplier { get; set; } = 2.0;

    /// <summary>
    /// 最大重试间隔（毫秒）
    /// </summary>
    public int MaxRetryIntervalMs { get; set; } = 30000;

    /// <summary>
    /// 计算第 N 次重试的等待时间
    /// </summary>
    public int CalculateRetryDelay(int attemptNumber)
    {
        if (!UseExponentialBackoff)
            return RetryIntervalMs;

        var delay = RetryIntervalMs * Math.Pow(BackoffMultiplier, attemptNumber - 1);
        return (int)Math.Min(delay, MaxRetryIntervalMs);
    }

    /// <summary>
    /// 默认策略
    /// </summary>
    public static RetryPolicy Default => new();

    /// <summary>
    /// 不重试策略
    /// </summary>
    public static RetryPolicy NoRetry => new() { MaxRetries = 0 };

    /// <summary>
    /// 激进重试策略（快速重试）
    /// </summary>
    public static RetryPolicy Aggressive => new()
    {
        MaxRetries = 5,
        RetryIntervalMs = 100,
        UseExponentialBackoff = false
    };
}

/// <summary>
/// 超时策略
/// </summary>
public class TimeoutPolicy
{
    /// <summary>
    /// 超时时间（毫秒）
    /// </summary>
    public int TimeoutMs { get; set; } = 30000;

    /// <summary>
    /// 是否启用超时
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 超时后是否取消执行
    /// </summary>
    public bool CancelOnTimeout { get; set; } = true;

    /// <summary>
    /// 创建 CancellationTokenSource（带超时）
    /// </summary>
    public CancellationTokenSource CreateCancellationTokenSource(CancellationToken parentToken)
    {
        if (!Enabled)
            return CancellationTokenSource.CreateLinkedTokenSource(parentToken);

        var cts = CancellationTokenSource.CreateLinkedTokenSource(parentToken);
        cts.CancelAfter(TimeoutMs);
        return cts;
    }

    /// <summary>
    /// 默认策略（30秒超时）
    /// </summary>
    public static TimeoutPolicy Default => new() { TimeoutMs = 30000 };

    /// <summary>
    /// 无超时策略
    /// </summary>
    public static TimeoutPolicy NoTimeout => new() { Enabled = false };

    /// <summary>
    /// 严格策略（5秒超时）
    /// </summary>
    public static TimeoutPolicy Strict => new() { TimeoutMs = 5000 };

    /// <summary>
    /// 宽松策略（5分钟超时）
    /// </summary>
    public static TimeoutPolicy Lenient => new() { TimeoutMs = 300000 };
}

/// <summary>
/// 跳过条件
/// </summary>
public class SkipCondition
{
    /// <summary>
    /// 条件名称
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// 条件表达式（如 "Context.HasPreviousError"）
    /// </summary>
    public string? Expression { get; set; }

    /// <summary>
    /// 委托条件判断
    /// </summary>
    public Func<FlowContext, bool>? Predicate { get; set; }

    /// <summary>
    /// 跳过原因
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// 评估是否应该跳过
    /// </summary>
    public bool Evaluate(FlowContext context)
    {
        if (Predicate != null)
            return Predicate(context);

        // 如果没有谓词，默认不跳过
        return false;
    }

    /// <summary>
    /// 创建基于条件的跳过条件
    /// </summary>
    public static SkipCondition When(Func<FlowContext, bool> predicate, string reason) =>
        new() { Predicate = predicate, Reason = reason };

    /// <summary>
    /// 总是跳过
    /// </summary>
    public static SkipCondition Always(string reason) =>
        new() { Predicate = _ => true, Reason = reason };

    /// <summary>
    /// 从不跳过
    /// </summary>
    public static SkipCondition Never =>
        new() { Predicate = _ => false };
}

/// <summary>
/// 节点执行配置
/// </summary>
public class NodeExecutionOptions
{
    /// <summary>
    /// 重试策略
    /// </summary>
    public RetryPolicy RetryPolicy { get; set; } = RetryPolicy.Default;

    /// <summary>
    /// 超时策略
    /// </summary>
    public TimeoutPolicy TimeoutPolicy { get; set; } = TimeoutPolicy.Default;

    /// <summary>
    /// 跳过条件列表
    /// </summary>
    public List<SkipCondition> SkipConditions { get; set; } = new();

    /// <summary>
    /// 是否允许部分成功（继续执行下游节点）
    /// </summary>
    public bool AllowPartialSuccess { get; set; } = false;

    /// <summary>
    /// 失败时是否中断整个 Flow
    /// </summary>
    public bool FailFlowOnError { get; set; } = true;

    /// <summary>
    /// 执行前延迟（毫秒）
    /// </summary>
    public int PreDelayMs { get; set; } = 0;

    /// <summary>
    /// 执行后延迟（毫秒）
    /// </summary>
    public int PostDelayMs { get; set; } = 0;

    /// <summary>
    /// 默认配置
    /// </summary>
    public static NodeExecutionOptions Default => new();

    /// <summary>
    /// 严格的配置（快速失败）
    /// </summary>
    public static NodeExecutionOptions Strict => new()
    {
        RetryPolicy = RetryPolicy.NoRetry,
        TimeoutPolicy = TimeoutPolicy.Strict,
        FailFlowOnError = true
    };

    /// <summary>
    /// 宽松的配置（允许重试）
    /// </summary>
    public static NodeExecutionOptions Lenient => new()
    {
        RetryPolicy = new RetryPolicy { MaxRetries = 5 },
        TimeoutPolicy = TimeoutPolicy.Lenient,
        FailFlowOnError = false
    };
}

/// <summary>
/// 节点执行上下文
/// </summary>
public class NodeExecutionContext
{
    public string NodeId { get; }
    public string NodeType { get; }
    public FlowContext FlowContext { get; }
    public NodeExecutionOptions Options { get; }

    /// <summary>
    /// 当前尝试次数
    /// </summary>
    public int CurrentAttempt { get; internal set; } = 1;

    /// <summary>
    /// 状态变更回调
    /// </summary>
    public Action<NodeExecutionStatus, NodeExecutionStatus>? OnStateChanged { get; set; }

    /// <summary>
    /// 是否还有重试次数
    /// </summary>
    public bool HasMoreRetries => CurrentAttempt <= Options.RetryPolicy.MaxRetries;

    public NodeExecutionContext(string nodeId, string nodeType, FlowContext flowContext, NodeExecutionOptions? options = null)
    {
        NodeId = nodeId;
        NodeType = nodeType;
        FlowContext = flowContext;
        Options = options ?? NodeExecutionOptions.Default;
    }
}

/// <summary>
/// 节点状态机 - 工业级实现
/// </summary>
public class NodeStateMachine
{
    private readonly NodeExecutionContext _context;
    private NodeExecutionStatus _status = NodeExecutionStatus.Ready;
    private readonly object _stateLock = new();

    public NodeExecutionStatus Status
    {
        get { lock (_stateLock) return _status; }
    }

    /// <summary>
    /// 状态历史
    /// </summary>
    public List<NodeStateHistory> StateHistory { get; } = new();

    /// <summary>
    /// 最后一次错误
    /// </summary>
    public Exception? LastError { get; private set; }

    /// <summary>
    /// 开始时间
    /// </summary>
    public DateTime? StartTime { get; private set; }

    /// <summary>
    /// 结束时间
    /// </summary>
    public DateTime? EndTime { get; private set; }

    /// <summary>
    /// 执行时长
    /// </summary>
    public TimeSpan? Duration => EndTime.HasValue && StartTime.HasValue
        ? EndTime.Value - StartTime.Value
        : null;

    public event EventHandler<NodeStateChangedEventArgs>? StateChanged;

    public NodeStateMachine(NodeExecutionContext context)
    {
        _context = context;
        RecordState(NodeExecutionStatus.Ready, "Node created");
    }

    /// <summary>
    /// 尝试状态转换
    /// </summary>
    public bool TryTransitionTo(NodeExecutionStatus newStatus, string? reason = null)
    {
        lock (_stateLock)
        {
            if (!NodeStateTransitions.CanTransition(_status, newStatus))
                return false;

            var oldStatus = _status;
            _status = newStatus;

            // 更新时间戳
            if (newStatus == NodeExecutionStatus.Running && !StartTime.HasValue)
                StartTime = DateTime.UtcNow;

            if (newStatus is NodeExecutionStatus.Success or NodeExecutionStatus.Failed or NodeExecutionStatus.Skipped)
                EndTime = DateTime.UtcNow;

            RecordState(newStatus, reason);
            OnStateChanged(oldStatus, newStatus);

            return true;
        }
    }

    /// <summary>
    /// 评估是否应该跳过
    /// </summary>
    public bool ShouldSkip()
    {
        foreach (var condition in _context.Options.SkipConditions)
        {
            if (condition.Evaluate(_context.FlowContext))
            {
                TryTransitionTo(NodeExecutionStatus.Skipped, condition.Reason);
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 执行节点（带状态机管理）
    /// </summary>
    public async Task<NodeResult> ExecuteAsync(Func<CancellationToken, Task<NodeResult>> executeFunc)
    {
        // 检查跳过条件
        if (ShouldSkip())
        {
            return NodeResult.Skipped(_context.Options.SkipConditions.FirstOrDefault(c =>
                c.Evaluate(_context.FlowContext))?.Reason ?? "Skipped by condition");
        }

        // 进入 Running 状态
        if (!TryTransitionTo(NodeExecutionStatus.Running, "Starting execution"))
            return NodeResult.Fail("Cannot transition to Running state");

        // 执行前延迟
        if (_context.Options.PreDelayMs > 0)
            await Task.Delay(_context.Options.PreDelayMs, _context.FlowContext.Token);

        // 创建超时控制
        using var timeoutCts = _context.Options.TimeoutPolicy.CreateCancellationTokenSource(_context.FlowContext.Token);

        while (true)
        {
            try
            {
                var result = await executeFunc(timeoutCts.Token);

                // 执行后延迟
                if (_context.Options.PostDelayMs > 0)
                    await Task.Delay(_context.Options.PostDelayMs, _context.FlowContext.Token);

                if (result.Success)
                {
                    TryTransitionTo(NodeExecutionStatus.Success, "Execution completed successfully");
                    return result;
                }
                else
                {
                    // 执行失败，检查是否需要重试
                    if (_context.HasMoreRetries && _context.Options.RetryPolicy.MaxRetries > 0)
                    {
                        LastError = new Exception(result.ErrorMessage ?? "Unknown error");
                        await HandleRetryAsync();
                        continue; // 重试
                    }
                    else
                    {
                        TryTransitionTo(NodeExecutionStatus.Failed, result.ErrorMessage ?? "Execution failed");
                        return result;
                    }
                }
            }
            catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested && !_context.FlowContext.Token.IsCancellationRequested)
            {
                // 超时取消
                LastError = new TimeoutException($"Node execution timed out after {_context.Options.TimeoutPolicy.TimeoutMs}ms");

                if (_context.HasMoreRetries)
                {
                    await HandleRetryAsync();
                    continue; // 重试
                }

                if (_context.Options.TimeoutPolicy.CancelOnTimeout)
                {
                    TryTransitionTo(NodeExecutionStatus.Failed, "Timeout");
                    return NodeResult.Fail($"Timeout after {_context.Options.TimeoutPolicy.TimeoutMs}ms");
                }

                // 不取消，返回失败
                TryTransitionTo(NodeExecutionStatus.Failed, "Timeout (ignored)");
                return NodeResult.Fail("Timeout");
            }
            catch (Exception ex)
            {
                LastError = ex;

                if (_context.HasMoreRetries && _context.Options.RetryPolicy.MaxRetries > 0)
                {
                    await HandleRetryAsync();
                    continue; // 重试
                }
                else
                {
                    TryTransitionTo(NodeExecutionStatus.Failed, ex.Message);
                    return NodeResult.Fail(ex.Message);
                }
            }
        }
    }

    private async Task HandleRetryAsync()
    {
        _context.CurrentAttempt++;
        var delay = _context.Options.RetryPolicy.CalculateRetryDelay(_context.CurrentAttempt - 1);

        TryTransitionTo(NodeExecutionStatus.Retrying, $"Retrying in {delay}ms (attempt {_context.CurrentAttempt}/{_context.Options.RetryPolicy.MaxRetries + 1})");

        if (delay > 0)
            await Task.Delay(delay, _context.FlowContext.Token);

        // 回到 Running 状态准备重试
        TryTransitionTo(NodeExecutionStatus.Running, $"Retry attempt {_context.CurrentAttempt}");
    }

    private void RecordState(NodeExecutionStatus status, string? reason)
    {
        StateHistory.Add(new NodeStateHistory
        {
            Timestamp = DateTime.UtcNow,
            Status = status,
            Reason = reason
        });
    }

    private void OnStateChanged(NodeExecutionStatus oldStatus, NodeExecutionStatus newStatus)
    {
        _context.OnStateChanged?.Invoke(oldStatus, newStatus);
        StateChanged?.Invoke(this, new NodeStateChangedEventArgs
        {
            NodeId = _context.NodeId,
            OldStatus = oldStatus,
            NewStatus = newStatus,
            Timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// 获取状态快照
    /// </summary>
    public NodeStateSnapshot GetSnapshot() => new()
    {
        NodeId = _context.NodeId,
        Status = Status,
        StartTime = StartTime,
        EndTime = EndTime,
        Duration = Duration,
        CurrentAttempt = _context.CurrentAttempt,
        MaxRetries = _context.Options.RetryPolicy.MaxRetries,
        LastError = LastError?.Message
    };
}

/// <summary>
/// 节点状态历史记录
/// </summary>
public class NodeStateHistory
{
    public DateTime Timestamp { get; set; }
    public NodeExecutionStatus Status { get; set; }
    public string? Reason { get; set; }
}

/// <summary>
/// 节点状态变更事件参数
/// </summary>
public class NodeStateChangedEventArgs : EventArgs
{
    public string NodeId { get; set; } = "";
    public NodeExecutionStatus OldStatus { get; set; }
    public NodeExecutionStatus NewStatus { get; set; }
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// 节点状态快照
/// </summary>
public class NodeStateSnapshot
{
    public string NodeId { get; set; } = "";
    public NodeExecutionStatus Status { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public TimeSpan? Duration { get; set; }
    public int CurrentAttempt { get; set; }
    public int MaxRetries { get; set; }
    public string? LastError { get; set; }
}
