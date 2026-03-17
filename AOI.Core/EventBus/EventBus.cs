using System.Collections.Concurrent;

namespace AOI.Core.EventBus;

public static class EventBus
{
    private static readonly ConcurrentDictionary<Type, List<Delegate>> _handlers = new();

    private static readonly object _lock = new();

    public static void Subscribe<T>(Action<T> handler)
    {
        lock (_lock)
        {
            var type = typeof(T);

            if (!_handlers.ContainsKey(type))
                _handlers[type] = new List<Delegate>();

            _handlers[type].Add(handler);
        }
    }

    public static void Publish<T>(T message)
    {
        if (_handlers.TryGetValue(typeof(T), out var handlers))
        {
            foreach (var handler in handlers.ToArray())
            {
                ((Action<T>)handler)?.Invoke(message);
            }
        }
    }

    public static void Clear()
    {
        _handlers.Clear();
    }
}