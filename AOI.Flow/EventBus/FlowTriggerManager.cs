using AOI.Device.Manager;
using AOI.Flow.Engine;
using AOI.Flow.Model;
using AOI.Flow.Recipe;

namespace AOI.Flow.EventBus;

/// <summary>
/// Flow触发器配置
/// </summary>
public class FlowTriggerConfig
{
    /// <summary>
    /// 触发器ID
    /// </summary>
    public string TriggerId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 触发器名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 监听的Flow Definition名称
    /// </summary>
    public string FlowDefinitionName { get; set; } = string.Empty;

    /// <summary>
    /// 事件过滤条件
    /// </summary>
    public TriggerFilter Filter { get; set; } = new();

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 触发冷却时间(ms) - 防止重复触发
    /// </summary>
    public int CooldownMs { get; set; } = 100;

    /// <summary>
    /// 最大并发执行数
    /// </summary>
    public int MaxConcurrentExecutions { get; set; } = 1;

    /// <summary>
    /// 优先级（数字越小优先级越高）
    /// </summary>
    public int Priority { get; set; } = 0;

    /// <summary>
    /// 触发时使用的Recipe ID（可选，如果不指定则使用事件中的RecipeId）
    /// </summary>
    public string? DefaultRecipeId { get; set; }

    /// <summary>
    /// 是否等待Flow执行完成
    /// </summary>
    public bool WaitForCompletion { get; set; } = false;

    /// <summary>
    /// 超时时间(ms)
    /// </summary>
    public int TimeoutMs { get; set; } = 30000;
}

/// <summary>
/// 触发器过滤条件
/// </summary>
public class TriggerFilter
{
    /// <summary>
    /// 监听的事件类型（完整类型名）
    /// </summary>
    public string EventTypeName { get; set; } = string.Empty;

    /// <summary>
    /// 事件来源过滤（设备ID等）
    /// </summary>
    public List<string>? SourceWhitelist { get; set; }

    /// <summary>
    /// Recipe ID过滤
    /// </summary>
    public List<string>? RecipeIdWhitelist { get; set; }

    /// <summary>
    /// 自定义条件表达式
    /// </summary>
    public Dictionary<string, object>? Conditions { get; set; }

    /// <summary>
    /// 检查事件是否匹配过滤条件
    /// </summary>
    public bool Matches(IFlowEvent @event)
    {
        // 检查事件类型
        if (!string.IsNullOrEmpty(EventTypeName))
        {
            var eventType = @event.GetType().Name;
            if (!EventTypeName.Split(',').Any(t => t.Trim().Equals(eventType, StringComparison.OrdinalIgnoreCase)))
                return false;
        }

        // 检查来源
        if (SourceWhitelist?.Count > 0)
        {
            if (!SourceWhitelist.Contains(@event.Source))
                return false;
        }

        // 检查Recipe
        if (RecipeIdWhitelist?.Count > 0)
        {
            if (@event.RecipeId == null || !RecipeIdWhitelist.Contains(@event.RecipeId))
                return false;
        }

        return true;
    }
}

/// <summary>
/// Flow触发器管理器 - 将事件与Flow执行关联
/// </summary>
public class FlowTriggerManager : IDisposable
{
    private readonly IFlowEventBus _eventBus;
    private readonly FlowEngine _flowEngine;
    private readonly RecipeManager _recipeManager;
    private readonly List<FlowTriggerConfig> _triggers = new();
    private readonly Dictionary<string, List<IDisposable>> _subscriptions = new();
    private readonly Dictionary<string, DateTimeOffset> _lastTriggerTimes = new();
    private readonly Dictionary<string, SemaphoreSlim> _triggerLocks = new();
    private readonly object _lock = new();

    public FlowTriggerManager(
        IFlowEventBus eventBus,
        FlowEngine flowEngine,
        RecipeManager recipeManager)
    {
        _eventBus = eventBus;
        _flowEngine = flowEngine;
        _recipeManager = recipeManager;
    }

    /// <summary>
    /// 注册触发器
    /// </summary>
    public void RegisterTrigger(FlowTriggerConfig config)
    {
        lock (_lock)
        {
            // 移除已存在的同名触发器
            UnregisterTrigger(config.TriggerId);

            _triggers.Add(config);
            var subs = new List<IDisposable>();

            // 根据事件类型订阅
            var eventType = Type.GetType(config.Filter.EventTypeName);
            if (eventType == null)
            {
                // 尝试从当前程序集查找
                eventType = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => a.GetTypes())
                    .FirstOrDefault(t => t.Name == config.Filter.EventTypeName && typeof(IFlowEvent).IsAssignableFrom(t));
            }

            if (eventType != null)
            {
                var subscribeMethod = typeof(IFlowEventBus).GetMethod("Subscribe")
                    ?.MakeGenericMethod(eventType);

                if (subscribeMethod != null)
                {
                    // 创建处理器委托
                    var handler = CreateHandler(config);
                    var sub = subscribeMethod.Invoke(_eventBus, new object[] { handler }) as IDisposable;
                    if (sub != null)
                        subs.Add(sub);
                }
            }

            _subscriptions[config.TriggerId] = subs;
            _triggerLocks[config.TriggerId] = new SemaphoreSlim(config.MaxConcurrentExecutions);
        }
    }

    /// <summary>
    /// 注销触发器
    /// </summary>
    public void UnregisterTrigger(string triggerId)
    {
        lock (_lock)
        {
            var trigger = _triggers.FirstOrDefault(t => t.TriggerId == triggerId);
            if (trigger != null)
            {
                _triggers.Remove(trigger);
            }

            if (_subscriptions.TryGetValue(triggerId, out var subs))
            {
                foreach (var sub in subs)
                    sub.Dispose();
                _subscriptions.Remove(triggerId);
            }

            if (_triggerLocks.TryGetValue(triggerId, out var semaphore))
            {
                semaphore.Dispose();
                _triggerLocks.Remove(triggerId);
            }
        }
    }

    /// <summary>
    /// 获取所有触发器配置
    /// </summary>
    public IReadOnlyList<FlowTriggerConfig> GetTriggers()
    {
        lock (_lock)
        {
            return _triggers.ToList().AsReadOnly();
        }
    }

    private Func<IFlowEvent, CancellationToken, Task> CreateHandler(FlowTriggerConfig config)
    {
        return async (@event, token) =>
        {
            if (!config.IsEnabled)
                return;

            // 检查过滤条件
            if (!config.Filter.Matches(@event))
                return;

            var lockObj = _triggerLocks.GetValueOrDefault(config.TriggerId);
            if (lockObj == null)
                return;

            // 检查冷却时间
            var now = DateTimeOffset.UtcNow;
            if (_lastTriggerTimes.TryGetValue(config.TriggerId, out var lastTime))
            {
                if ((now - lastTime).TotalMilliseconds < config.CooldownMs)
                    return;
            }

            // 获取并发许可
            if (!await lockObj.WaitAsync(0, token))
                return;

            try
            {
                _lastTriggerTimes[config.TriggerId] = now;

                // 确定使用的Recipe
                var recipeId = config.DefaultRecipeId ?? @event.RecipeId;
                var recipe = recipeId != null
                    ? _recipeManager.GetRecipe(recipeId)
                    : _recipeManager.GetDefaultRecipe();

                if (recipe == null)
                {
                    // 记录错误：未找到Recipe
                    return;
                }

                // 创建Flow定义（基于Recipe）
                var flowDefinition = _recipeManager.CreateFlowDefinitionFromRecipe(recipe);

                // 使用CTS设置超时
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
                cts.CancelAfter(config.TimeoutMs);

                // 启动Flow
                var instance = await _flowEngine.StartFlowAsync(flowDefinition, cts.Token);

                // 可选：等待完成
                if (config.WaitForCompletion)
                {
                    await _flowEngine.Scheduler.WaitForFlowAsync(instance.Id);
                }
            }
            finally
            {
                lockObj.Release();
            }
        };
    }

    public void Dispose()
    {
        lock (_lock)
        {
            foreach (var triggerId in _subscriptions.Keys.ToList())
            {
                UnregisterTrigger(triggerId);
            }
        }
    }
}

/// <summary>
/// 设备触发器 - 监听设备信号并触发Flow
/// </summary>
public class DeviceFlowTrigger
{
    private readonly IFlowEventBus _eventBus;
    private readonly FlowTriggerManager _triggerManager;

    public DeviceFlowTrigger(IFlowEventBus eventBus, FlowTriggerManager triggerManager)
    {
        _eventBus = eventBus;
        _triggerManager = triggerManager;
    }

    /// <summary>
    /// 创建标准设备触发器配置
    /// </summary>
    public FlowTriggerConfig CreateDeviceTrigger(
        string name,
        string flowDefinitionName,
        string deviceId,
        TriggerSignalType signalType,
        string? recipeId = null)
    {
        return new FlowTriggerConfig
        {
            Name = name,
            FlowDefinitionName = flowDefinitionName,
            Filter = new TriggerFilter
            {
                EventTypeName = "DeviceTriggerEvent",
                SourceWhitelist = new List<string> { deviceId },
                RecipeIdWhitelist = recipeId != null ? new List<string> { recipeId } : null,
                Conditions = new Dictionary<string, object>
                {
                    ["SignalType"] = signalType.ToString()
                }
            },
            DefaultRecipeId = recipeId,
            CooldownMs = 500, // 设备触发通常需要更长冷却
            MaxConcurrentExecutions = 1
        };
    }
}

/// <summary>
/// AOI检测触发器 - 监听AOI结果并触发Flow
/// </summary>
public class AoiFlowTrigger
{
    private readonly IFlowEventBus _eventBus;
    private readonly FlowTriggerManager _triggerManager;

    public AoiFlowTrigger(IFlowEventBus eventBus, FlowTriggerManager triggerManager)
    {
        _eventBus = eventBus;
        _triggerManager = triggerManager;
    }

    /// <summary>
    /// 创建NG品处理触发器
    /// </summary>
    public FlowTriggerConfig CreateNgHandlingTrigger(
        string name,
        string flowDefinitionName,
        string? cameraId = null,
        string? recipeId = null)
    {
        return new FlowTriggerConfig
        {
            Name = name,
            FlowDefinitionName = flowDefinitionName,
            Filter = new TriggerFilter
            {
                EventTypeName = "AoiInspectionResultEvent",
                SourceWhitelist = cameraId != null ? new List<string> { cameraId } : null,
                RecipeIdWhitelist = recipeId != null ? new List<string> { recipeId } : null
            },
            DefaultRecipeId = recipeId,
            CooldownMs = 100
        };
    }
}
