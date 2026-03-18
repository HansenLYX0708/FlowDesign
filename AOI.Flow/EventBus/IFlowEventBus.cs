namespace AOI.Flow.EventBus;

/// <summary>
/// 工业级事件总线接口 - 支持设备触发和AOI触发
/// </summary>
public interface IFlowEventBus
{
    /// <summary>
    /// 发布事件到总线
    /// </summary>
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken token = default) where TEvent : class, IFlowEvent;

    /// <summary>
    /// 订阅特定类型的事件
    /// </summary>
    IDisposable Subscribe<TEvent>(IFlowEventHandler<TEvent> handler) where TEvent : class, IFlowEvent;

    /// <summary>
    /// 订阅特定类型的事件（使用委托）
    /// </summary>
    IDisposable Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler) where TEvent : class, IFlowEvent;

    /// <summary>
    /// 订阅带过滤条件的事件
    /// </summary>
    IDisposable Subscribe<TEvent>(Func<TEvent, bool> predicate, IFlowEventHandler<TEvent> handler) where TEvent : class, IFlowEvent;

    /// <summary>
    /// 获取事件统计信息
    /// </summary>
    EventBusStatistics GetStatistics();

    /// <summary>
    /// 清空所有订阅
    /// </summary>
    void ClearSubscriptions();
}

/// <summary>
/// 事件基类接口
/// </summary>
public interface IFlowEvent
{
    /// <summary>
    /// 事件唯一标识
    /// </summary>
    Guid EventId { get; }

    /// <summary>
    /// 事件发生时间戳
    /// </summary>
    DateTimeOffset Timestamp { get; }

    /// <summary>
    /// 事件来源（设备ID、AOI模块等）
    /// </summary>
    string Source { get; }

    /// <summary>
    /// 关联的Recipe ID（可选）
    /// </summary>
    string? RecipeId { get; }

    /// <summary>
    /// 关联的产品ID（可选）
    /// </summary>
    string? ProductId { get; }
}

/// <summary>
/// 事件处理器接口
/// </summary>
public interface IFlowEventHandler<in TEvent> where TEvent : class, IFlowEvent
{
    Task HandleAsync(TEvent @event, CancellationToken token);
}

/// <summary>
/// 事件总线统计信息
/// </summary>
public class EventBusStatistics
{
    public int TotalSubscribers { get; set; }
    public long TotalEventsPublished { get; set; }
    public long TotalEventsHandled { get; set; }
    public long FailedHandlers { get; set; }
    public Dictionary<string, int> SubscribersByEventType { get; set; } = new();
}
