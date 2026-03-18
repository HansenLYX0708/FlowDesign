using AOI.Core.Logging;
using AOI.Flow.EventBus;
using AOI.Flow.Model;
using AOI.Flow.Recipe;

namespace AOI.Flow.Node;

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

    public async Task<NodeResult> ExecuteAsync(FlowContext context)
    {
        var startTime = DateTime.UtcNow;
        NodeResult result;

        try
        {
            Logger.Info($"Node Start {Id} ({DisplayName})");

            // 发布节点开始事件
            await PublishNodeStartedAsync(context);

            result = await OnExecute(context);

            // 发布节点完成事件
            await PublishNodeCompletedAsync(context, result, startTime);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"FlowNode {Id} Error");

            result = NodeResult.Fail(ex.Message);

            // 发布节点失败事件
            await PublishNodeCompletedAsync(context, result, startTime);
        }

        return result;
    }

    protected abstract Task<NodeResult> OnExecute(
        FlowContext context);

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

        // TODO: 发布节点开始事件
        return Task.CompletedTask;
    }

    /// <summary>
    /// 发布节点完成事件
    /// </summary>
    private async Task PublishNodeCompletedAsync(
        FlowContext context,
        NodeResult result,
        DateTime startTime)
    {
        if (context.EventBus == null) return;

        var executionTimeMs = (DateTime.UtcNow - startTime).TotalMilliseconds;

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