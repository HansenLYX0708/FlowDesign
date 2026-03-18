using System.Collections.Concurrent;
using System.Threading.Channels;

namespace AOI.Flow.EventBus;

/// <summary>
/// 工业级事件总线实现 - 支持高并发、异步处理、背压控制
/// </summary>
public class FlowEventBus : IFlowEventBus, IDisposable
{
    private readonly ConcurrentDictionary<Type, List<Subscription>> _subscriptions = new();
    private readonly Channel<EventWrapper> _eventChannel;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _processingTask;
    private readonly int _maxConcurrentHandlers;
    private long _totalEventsPublished;
    private long _totalEventsHandled;
    private long _failedHandlers;
    private bool _disposed;

    public FlowEventBus(int channelCapacity = 10000, int maxConcurrentHandlers = 100)
    {
        _maxConcurrentHandlers = maxConcurrentHandlers;
        _eventChannel = Channel.CreateBounded<EventWrapper>(new BoundedChannelOptions(channelCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });

        // 启动后台处理任务
        _processingTask = Task.Run(ProcessEventsAsync);
    }

    public Task PublishAsync<TEvent>(TEvent @event, CancellationToken token = default) where TEvent : class, IFlowEvent
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(FlowEventBus));

        Interlocked.Increment(ref _totalEventsPublished);
        var wrapper = new EventWrapper(@event, typeof(TEvent));
        return _eventChannel.Writer.WriteAsync(wrapper, token).AsTask();
    }

    public IDisposable Subscribe<TEvent>(IFlowEventHandler<TEvent> handler) where TEvent : class, IFlowEvent
    {
        return SubscribeInternal<TEvent>((e, t) => handler.HandleAsync(e, t));
    }

    public IDisposable Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler) where TEvent : class, IFlowEvent
    {
        return SubscribeInternal(handler);
    }

    public IDisposable Subscribe<TEvent>(Func<TEvent, bool> predicate, IFlowEventHandler<TEvent> handler) where TEvent : class, IFlowEvent
    {
        return SubscribeInternal<TEvent>(async (e, t) =>
        {
            if (predicate(e))
                await handler.HandleAsync(e, t);
        });
    }

    private IDisposable SubscribeInternal<TEvent>(Func<TEvent, CancellationToken, Task> handler) where TEvent : class, IFlowEvent
    {
        var eventType = typeof(TEvent);
        Action onDispose = () => { }; // 临时占位

        var subscription = new Subscription(
            async (obj, token) => await handler((TEvent)obj, token),
            onDispose);

        // 创建真正的 dispose 回调
        onDispose = () => Unsubscribe(eventType, subscription);
        subscription.UpdateOnDispose(onDispose);

        _subscriptions.AddOrUpdate(
            eventType,
            _ => new List<Subscription> { subscription },
            (_, list) =>
            {
                lock (list)
                {
                    list.Add(subscription);
                }
                return list;
            });

        return subscription;
    }

    private void Unsubscribe(Type eventType, Subscription subscription)
    {
        if (_subscriptions.TryGetValue(eventType, out var list))
        {
            lock (list)
            {
                list.Remove(subscription);
            }
        }
    }

    private async Task ProcessEventsAsync()
    {
        await foreach (var wrapper in _eventChannel.Reader.ReadAllAsync(_cts.Token))
        {
            if (_subscriptions.TryGetValue(wrapper.EventType, out var handlers))
            {
                List<Subscription> snapshot;
                lock (handlers)
                {
                    snapshot = handlers.ToList();
                }

                // 并行执行所有处理器，使用SemaphoreSlim限制并发
                using var semaphore = new SemaphoreSlim(_maxConcurrentHandlers);
                var tasks = snapshot.Select(async sub =>
                {
                    await semaphore.WaitAsync(_cts.Token);
                    try
                    {
                        await sub.Handler(wrapper.Event, _cts.Token);
                        Interlocked.Increment(ref _totalEventsHandled);
                    }
                    catch (Exception)
                    {
                        Interlocked.Increment(ref _failedHandlers);
                        // 继续处理其他handler，不中断
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }).ToList();

                await Task.WhenAll(tasks);
            }
        }
    }

    public EventBusStatistics GetStatistics()
    {
        var stats = new EventBusStatistics
        {
            TotalEventsPublished = Interlocked.Read(ref _totalEventsPublished),
            TotalEventsHandled = Interlocked.Read(ref _totalEventsHandled),
            FailedHandlers = Interlocked.Read(ref _failedHandlers)
        };

        foreach (var kvp in _subscriptions)
        {
            lock (kvp.Value)
            {
                stats.SubscribersByEventType[kvp.Key.Name] = kvp.Value.Count;
                stats.TotalSubscribers += kvp.Value.Count;
            }
        }

        return stats;
    }

    public void ClearSubscriptions()
    {
        _subscriptions.Clear();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _cts.Cancel();
        _eventChannel.Writer.Complete();
        _processingTask.Wait(TimeSpan.FromSeconds(5));
        _cts.Dispose();
        ClearSubscriptions();
    }

    private class Subscription : IDisposable
    {
        public Func<object, CancellationToken, Task> Handler { get; }
        private Action _onDispose;

        public Subscription(Func<object, CancellationToken, Task> handler, Action onDispose)
        {
            Handler = handler;
            _onDispose = onDispose;
        }

        public void UpdateOnDispose(Action onDispose)
        {
            _onDispose = onDispose;
        }

        public void Dispose()
        {
            _onDispose();
        }
    }

    private class EventWrapper
    {
        public object Event { get; }
        public Type EventType { get; }

        public EventWrapper(object @event, Type eventType)
        {
            Event = @event;
            EventType = eventType;
        }
    }
}
