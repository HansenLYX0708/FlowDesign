using AOI.Device.Manager;
using AOI.Flow.EventBus;
using AOI.Flow.Pipeline;
using AOI.Flow.Recipe;

namespace AOI.Flow.Model;

public class FlowContext
{
    public Dictionary<string, object> Data { get; } = new();

    public DeviceManager DeviceManager { get; }

    public PipelineQueue Pipeline { get; }

    public CancellationToken Token { get; }

    /// <summary>
    /// 当前使用的Recipe（产品配方）
    /// </summary>
    public Recipe.Recipe? Recipe { get; set; }

    /// <summary>
    /// 事件总线 - 用于发布和订阅事件
    /// </summary>
    public IFlowEventBus? EventBus { get; set; }

    /// <summary>
    /// 当前Flow实例ID
    /// </summary>
    public string FlowInstanceId { get; set; } = string.Empty;

    /// <summary>
    /// 产品ID（当前检测的产品）
    /// </summary>
    public string? ProductId { get; set; }

    public FlowContext(
        DeviceManager deviceManager,
        PipelineQueue pipeline,
        CancellationToken token)
    {
        DeviceManager = deviceManager;
        Pipeline = pipeline;
        Token = token;
    }

    public T Get<T>(string key)
    {
        return (T)Data[key];
    }

    public void Set(string key, object value)
    {
        Data[key] = value;
    }

    /// <summary>
    /// 尝试获取值，不存在时返回默认值
    /// </summary>
    public T? GetValueOrDefault<T>(string key, T? defaultValue = default)
    {
        if (Data.TryGetValue(key, out var value) && value is T t)
            return t;
        return defaultValue;
    }

    #region Recipe参数访问

    /// <summary>
    /// 获取全局参数值
    /// </summary>
    public ParameterValue? GetGlobalParameter(string key)
    {
        return Recipe?.GlobalParameters.GetValueOrDefault(key);
    }

    /// <summary>
    /// 获取全局参数值（带默认值）
    /// </summary>
    public T? GetGlobalParameter<T>(string key, T? defaultValue = default)
    {
        var param = GetGlobalParameter(key);
        if (param == null)
            return defaultValue;

        try
        {
            return param.As<T>();
        }
        catch
        {
            return defaultValue;
        }
    }

    /// <summary>
    /// 获取节点参数值
    /// </summary>
    public ParameterValue? GetNodeParameter(string nodeId, string key)
    {
        if (Recipe?.NodeParameters.TryGetValue(nodeId, out var nodeParams) == true)
        {
            return nodeParams.Config.GetValueOrDefault(key);
        }
        return null;
    }

    /// <summary>
    /// 获取节点参数值（带默认值）
    /// </summary>
    public T? GetNodeParameter<T>(string nodeId, string key, T? defaultValue = default)
    {
        var param = GetNodeParameter(nodeId, key);
        if (param == null)
            return defaultValue;

        try
        {
            return param.As<T>();
        }
        catch
        {
            return defaultValue;
        }
    }

    /// <summary>
    /// 获取相机配置
    /// </summary>
    public CameraConfig? GetCameraConfig(string cameraId)
    {
        return Recipe?.DeviceConfigs.Cameras.GetValueOrDefault(cameraId);
    }

    /// <summary>
    /// 获取光源配置
    /// </summary>
    public LightConfig? GetLightConfig(string lightId)
    {
        return Recipe?.DeviceConfigs.Lights.GetValueOrDefault(lightId);
    }

    /// <summary>
    /// 获取轴配置
    /// </summary>
    public AxisConfig? GetAxisConfig(string axisId)
    {
        return Recipe?.DeviceConfigs.Axes.GetValueOrDefault(axisId);
    }

    /// <summary>
    /// 获取轴位置预设
    /// </summary>
    public PositionPreset? GetAxisPosition(string axisId, string positionName)
    {
        var axis = GetAxisConfig(axisId);
        return axis?.Positions.FirstOrDefault(p => p.Name == positionName);
    }

    /// <summary>
    /// 获取检测项配置
    /// </summary>
    public InspectionItem? GetInspectionItem(string itemId)
    {
        return Recipe?.InspectionSpecs.Items.FirstOrDefault(i => i.Id == itemId);
    }

    #endregion

    #region 事件发布

    /// <summary>
    /// 发布事件到事件总线
    /// </summary>
    public async Task PublishEventAsync<TEvent>(TEvent @event) where TEvent : class, IFlowEvent
    {
        if (EventBus != null)
        {
            await EventBus.PublishAsync(@event, Token);
        }
    }

    #endregion
}