using AOI.Device.Manager;
using AOI.Flow.DAG;
using AOI.Flow.EventBus;
using AOI.Flow.Model;
using AOI.Flow.Pipeline;
using AOI.Flow.Recipe;

namespace AOI.Flow.Engine;

public class FlowEngine : IDisposable
{
    private readonly DeviceManager _deviceManager;
    private readonly FlowScheduler _scheduler;
    private readonly RecipeManager _recipeManager;
    private readonly IFlowEventBus _eventBus;
    private readonly FlowTriggerManager? _triggerManager;
    private bool _disposed;

    public FlowEngine(
        DeviceManager deviceManager,
        RecipeManager? recipeManager = null,
        IFlowEventBus? eventBus = null,
        int maxFlowConcurrency = 5,
        int maxNodeConcurrencyPerFlow = 10)
    {
        _deviceManager = deviceManager;
        _recipeManager = recipeManager ?? new RecipeManager();
        _eventBus = eventBus ?? new FlowEventBus();
        _scheduler = new FlowScheduler(maxFlowConcurrency, maxNodeConcurrencyPerFlow);

        // 如果提供了RecipeManager和EventBus，创建触发器管理器
        if (recipeManager != null && eventBus != null)
        {
            _triggerManager = new FlowTriggerManager(eventBus, this, recipeManager);
        }
    }

    public FlowScheduler Scheduler => _scheduler;
    public RecipeManager RecipeManager => _recipeManager;
    public IFlowEventBus EventBus => _eventBus;
    public FlowTriggerManager? TriggerManager => _triggerManager;

    /// <summary>
    /// 使用指定Recipe启动Flow
    /// </summary>
    public async Task<FlowInstance> StartFlowWithRecipeAsync(
        string recipeId,
        CancellationToken cancellationToken = default,
        string? productId = null)
    {
        var recipe = _recipeManager.GetRecipe(recipeId)
            ?? throw new ArgumentException($"Recipe '{recipeId}' not found");

        return await StartFlowWithRecipeAsync(recipe, cancellationToken, productId);
    }

    /// <summary>
    /// 使用指定Recipe启动Flow
    /// </summary>
    public async Task<FlowInstance> StartFlowWithRecipeAsync(
        Recipe.Recipe recipe,
        CancellationToken cancellationToken = default,
        string? productId = null)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(FlowEngine));

        // 从Recipe创建Flow定义
        var flowDefinition = _recipeManager.CreateFlowDefinitionFromRecipe(recipe);

        // 创建新的 PipelineQueue
        var pipeline = new PipelineQueue();

        // 创建 FlowContext，使用链接的取消令牌
        var context = new FlowContext(
            _deviceManager,
            pipeline,
            cancellationToken)
        {
            Recipe = recipe,
            EventBus = _eventBus,
            ProductId = productId
        };

        // 通过调度器启动 Flow
        var instance = await _scheduler.ScheduleFlowAsync(flowDefinition, context);

        // 设置Flow实例ID到Context
        context.FlowInstanceId = instance.Id;

        // 发布Flow开始事件
        await _eventBus.PublishAsync(new FlowStartedEvent
        {
            Source = instance.Id,
            RecipeId = recipe.Id,
            ProductId = productId,
            FlowDefinitionName = flowDefinition.Name,
            TriggerSource = "Manual"
        }, cancellationToken);

        // 在Flow完成后发布完成事件
        _ = Task.Run(async () =>
        {
            try
            {
                await _scheduler.WaitForFlowAsync(instance.Id);

                await _eventBus.PublishAsync(new FlowCompletedEvent
                {
                    Source = instance.Id,
                    RecipeId = recipe.Id,
                    ProductId = productId,
                    FlowDefinitionName = flowDefinition.Name,
                    IsSuccess = instance.Status == FlowStatus.Completed,
                    TotalExecutionTimeMs = instance.EndTime.HasValue
                        ? (instance.EndTime.Value - instance.StartTime!.Value).TotalMilliseconds
                        : 0
                }, CancellationToken.None);
            }
            catch (Exception ex)
            {
                await _eventBus.PublishAsync(new FlowCompletedEvent
                {
                    Source = instance.Id,
                    RecipeId = recipe.Id,
                    ProductId = productId,
                    FlowDefinitionName = flowDefinition.Name,
                    IsSuccess = false,
                    ErrorMessage = ex.Message
                }, CancellationToken.None);
            }
        });

        return instance;
    }

    /// <summary>
    /// 使用指定FlowDefinition启动Flow（不带Recipe）
    /// </summary>
    public async Task<FlowInstance> StartFlowAsync(
        FlowDefinition definition,
        CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(FlowEngine));

        // 创建新的 PipelineQueue
        var pipeline = new PipelineQueue();

        // 创建 FlowContext，使用链接的取消令牌
        var context = new FlowContext(
            _deviceManager,
            pipeline,
            cancellationToken)
        {
            EventBus = _eventBus
        };

        // 通过调度器启动 Flow
        return await _scheduler.ScheduleFlowAsync(definition, context);
    }

    /// <summary>
    /// 执行Flow并等待完成（使用Recipe）
    /// </summary>
    public async Task ExecuteFlowWithRecipeAsync(
        string recipeId,
        CancellationToken cancellationToken = default,
        string? productId = null)
    {
        var instance = await StartFlowWithRecipeAsync(recipeId, cancellationToken, productId);
        await _scheduler.WaitForFlowAsync(instance.Id);

        if (instance.Status == FlowStatus.Failed && instance.Error != null)
        {
            throw new AggregateException($"Flow '{instance.Id}' failed", instance.Error);
        }
    }

    /// <summary>
    /// 执行Flow并等待完成
    /// </summary>
    public async Task ExecuteFlowAsync(
        FlowDefinition definition,
        CancellationToken cancellationToken = default)
    {
        var instance = await StartFlowAsync(definition, cancellationToken);
        await _scheduler.WaitForFlowAsync(instance.Id);

        if (instance.Status == FlowStatus.Failed && instance.Error != null)
        {
            throw new AggregateException($"Flow '{instance.Id}' failed", instance.Error);
        }
    }

    /// <summary>
    /// 注册设备触发器
    /// </summary>
    public void RegisterDeviceTrigger(FlowTriggerConfig config)
    {
        _triggerManager?.RegisterTrigger(config);
    }

    /// <summary>
    /// 注册AOI触发器
    /// </summary>
    public void RegisterAoiTrigger(FlowTriggerConfig config)
    {
        _triggerManager?.RegisterTrigger(config);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _triggerManager?.Dispose();
        _scheduler.Dispose();
        if (_eventBus is IDisposable disposable)
            disposable.Dispose();
    }
}