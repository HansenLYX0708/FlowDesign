using AOI.Core.Logging;
using AOI.Flow.EventBus;
using AOI.Flow.Model;
using AOI.Flow.Recipe;

namespace AOI.Flow.Node;

/// <summary>
/// Flow 节点基类 - 工业级实现（支持状态机、重试、超时）
/// </summary>
public abstract class FlowNodeBase : IFlowNode
{
    public string Id { get; init; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 节点显示名称
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// 节点类型标识
    /// </summary>
    public virtual string NodeType => GetType().Name;

    /// <summary>
    /// 节点执行选项（可在子类中覆盖）
    /// </summary>
    protected virtual NodeExecutionOptions ExecutionOptions => NodeExecutionOptions.Default;

    public async Task<NodeResult> ExecuteAsync(FlowContext context)
    {
        var startTime = DateTime.UtcNow;

        // 创建节点执行上下文
        var nodeExecContext = new NodeExecutionContext(Id, NodeType, context, ExecutionOptions);

        // 创建状态机
        var stateMachine = new NodeStateMachine(nodeExecContext);

        // 绑定状态变更事件到 Flow 实例
        if (!string.IsNullOrEmpty(context.FlowInstanceId))
        {
            stateMachine.StateChanged += (s, e) =>
            {
                // 通知 FlowInstance 节点状态变更
                if (e.NewStatus == NodeExecutionStatus.Running)
                    context.Data[$"node:{Id}:start"] = DateTime.UtcNow;
                else if (e.NewStatus is NodeExecutionStatus.Success or NodeExecutionStatus.Failed or NodeExecutionStatus.Skipped)
                    context.Data[$"node:{Id}:end"] = DateTime.UtcNow;
            };
        }

        // 发布节点开始事件
        await PublishNodeStartedAsync(context);

        // 使用状态机执行节点
        var result = await stateMachine.ExecuteAsync(async (ct) =>
        {
            try
            {
                Logger.Info($"Node [{Id}] {DisplayName} - Executing...");
                var nodeResult = await OnExecute(context);

                // 记录执行数据
                nodeResult.RetryCount = nodeExecContext.CurrentAttempt - 1;
                nodeResult.ExecutionTimeMs = (DateTime.UtcNow - startTime).TotalMilliseconds;

                return nodeResult;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Node [{Id}] {DisplayName} - Execution error");
                return NodeResult.FromException(ex);
            }
        });

        // 发布节点完成事件
        var executionTimeMs = (DateTime.UtcNow - startTime).TotalMilliseconds;
        await PublishNodeCompletedAsync(context, result, executionTimeMs);

        return result;
    }

    protected abstract Task<NodeResult> OnExecute(FlowContext context);

    #region Recipe参数访问辅助方法

    /// <summary>
    /// 获取本节点的配置参数
    /// </summary>
    protected T? GetParameter<T>(FlowContext context, string key, T? defaultValue = default)
    {
        return context.GetNodeParameter<T>(Id, key, defaultValue);
    }

    /// <summary>
    /// 获取全局参数
    /// </summary>
    protected T? GetGlobalParameter<T>(FlowContext context, string key, T? defaultValue = default)
    {
        return context.GetGlobalParameter<T>(key, defaultValue);
    }

    /// <summary>
    /// 获取相机配置
    /// </summary>
    protected CameraConfig? GetCameraConfig(FlowContext context, string cameraId)
    {
        return context.GetCameraConfig(cameraId);
    }

    /// <summary>
    /// 获取光源配置
    /// </summary>
    protected LightConfig? GetLightConfig(FlowContext context, string lightId)
    {
        return context.GetLightConfig(lightId);
    }

    /// <summary>
    /// 获取轴配置
    /// </summary>
    protected AxisConfig? GetAxisConfig(FlowContext context, string axisId)
    {
        return context.GetAxisConfig(axisId);
    }

    /// <summary>
    /// 获取轴位置
    /// </summary>
    protected double? GetAxisPosition(FlowContext context, string axisId, string positionName)
    {
        return context.GetAxisPosition(axisId, positionName)?.Position;
    }

    #endregion

    #region 事件发布辅助方法

    /// <summary>
    /// 发布设备触发事件
    /// </summary>
    protected async Task PublishDeviceTriggerAsync(
        FlowContext context,
        string deviceId,
        TriggerSignalType signalType,
        object? signalValue = null)
    {
        if (context.EventBus == null) return;

        await context.EventBus.PublishAsync(new DeviceTriggerEvent
        {
            Source = deviceId,
            SignalType = signalType,
            SignalValue = signalValue,
            RecipeId = context.Recipe?.Id,
            ProductId = context.ProductId
        }, context.Token);
    }

    /// <summary>
    /// 发布AOI检测结果事件
    /// </summary>
    protected async Task PublishAoiResultAsync(
        FlowContext context,
        string cameraId,
        InspectionResult result,
        List<DefectInfo>? defects = null,
        double inspectionTimeMs = 0)
    {
        if (context.EventBus == null) return;

        await context.EventBus.PublishAsync(new AoiInspectionResultEvent
        {
            Source = cameraId,
            Result = result,
            Defects = defects,
            InspectionTimeMs = inspectionTimeMs,
            RecipeId = context.Recipe?.Id,
            ProductId = context.ProductId
        }, context.Token);
    }

    /// <summary>
    /// 发布图像采集完成事件
    /// </summary>
    protected async Task PublishImageAcquiredAsync(
        FlowContext context,
        string cameraId,
        object? imageData,
        string? imagePath,
        double acquisitionTimeMs = 0)
    {
        if (context.EventBus == null) return;

        await context.EventBus.PublishAsync(new ImageAcquiredEvent
        {
            Source = cameraId,
            ImageData = imageData,
            ImagePath = imagePath,
            AcquisitionTimeMs = acquisitionTimeMs,
            RecipeId = context.Recipe?.Id,
            ProductId = context.ProductId
        }, context.Token);
    }

    /// <summary>
    /// 发布节点开始执行事件
    /// </summary>
    private Task PublishNodeStartedAsync(FlowContext context)
    {
        if (context.EventBus == null) return Task.CompletedTask;

        return context.EventBus.PublishAsync(new FlowNodeStartedEvent
        {
            Source = Id,
            FlowInstanceId = context.FlowInstanceId,
            NodeType = NodeType,
            RecipeId = context.Recipe?.Id,
            ProductId = context.ProductId
        }, context.Token);
    }

    /// <summary>
    /// 发布节点完成事件
    /// </summary>
    private async Task PublishNodeCompletedAsync(
        FlowContext context,
        NodeResult result,
        double executionTimeMs)
    {
        if (context.EventBus == null) return;

        await context.EventBus.PublishAsync(new FlowNodeCompletedEvent
        {
            Source = Id,
            FlowInstanceId = context.FlowInstanceId,
            NodeType = NodeType,
            IsSuccess = result.Success,
            ExecutionTimeMs = executionTimeMs,
            ErrorMessage = result.ErrorMessage,
            RecipeId = context.Recipe?.Id,
            ProductId = context.ProductId
        }, context.Token);
    }

    #endregion
}