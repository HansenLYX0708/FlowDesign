namespace AOI.Flow.EventBus;

#region 设备触发事件

/// <summary>
/// 设备信号触发事件 - 如PLC触发、IO信号
/// </summary>
public class DeviceTriggerEvent : IFlowEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
    public string Source { get; set; } = string.Empty; // 设备ID
    public string? RecipeId { get; set; }
    public string? ProductId { get; set; }

    /// <summary>
    /// 触发信号类型
    /// </summary>
    public TriggerSignalType SignalType { get; set; }

    /// <summary>
    /// 信号值/状态
    /// </summary>
    public object? SignalValue { get; set; }

    /// <summary>
    /// 触发位置/工位
    /// </summary>
    public string? StationId { get; set; }

    /// <summary>
    /// 原始信号数据
    /// </summary>
    public Dictionary<string, object>? RawData { get; set; }
}

public enum TriggerSignalType
{
    None,
    DigitalInput,      // 数字量输入
    DigitalOutput,     // 数字量输出
    AnalogInput,       // 模拟量输入
    Encoder,           // 编码器
    PlcTrigger,        // PLC触发
    MotionComplete,    // 运动完成
    SensorDetect,      // 传感器检测
    ManualButton,      // 手动按钮
}

/// <summary>
/// 轴运动完成事件
/// </summary>
public class AxisMotionCompletedEvent : IFlowEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
    public string Source { get; set; } = string.Empty; // 轴ID
    public string? RecipeId { get; set; }
    public string? ProductId { get; set; }

    /// <summary>
    /// 目标位置
    /// </summary>
    public double TargetPosition { get; set; }

    /// <summary>
    /// 实际位置
    /// </summary>
    public double ActualPosition { get; set; }

    /// <summary>
    /// 位置误差
    /// </summary>
    public double PositionError => Math.Abs(ActualPosition - TargetPosition);

    /// <summary>
    /// 运动是否成功
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// 错误信息（如果失败）
    /// </summary>
    public string? ErrorMessage { get; set; }
}

#endregion

#region AOI检测事件

/// <summary>
/// AOI检测结果触发事件
/// </summary>
public class AoiInspectionResultEvent : IFlowEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
    public string Source { get; set; } = string.Empty; // 相机/光源ID
    public string? RecipeId { get; set; }
    public string? ProductId { get; set; }

    /// <summary>
    /// 检测结果
    /// </summary>
    public InspectionResult Result { get; set; }

    /// <summary>
    /// 缺陷列表
    /// </summary>
    public List<DefectInfo>? Defects { get; set; }

    /// <summary>
    /// 图像数据路径
    /// </summary>
    public string? ImagePath { get; set; }

    /// <summary>
    /// 检测耗时(ms)
    /// </summary>
    public double InspectionTimeMs { get; set; }

    /// <summary>
    /// 检测区域信息
    /// </summary>
    public InspectionRegion? Region { get; set; }
}

public enum InspectionResult
{
    Unknown,
    Pass,           // 通过
    Fail,           // 失败
    Warning,        // 警告
    Error,          // 错误
}

public class DefectInfo
{
    public string Type { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public double Confidence { get; set; }
    public string? Description { get; set; }
}

public class InspectionRegion
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public double Angle { get; set; }
}

/// <summary>
/// 图像采集完成事件
/// </summary>
public class ImageAcquiredEvent : IFlowEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
    public string Source { get; set; } = string.Empty; // 相机ID
    public string? RecipeId { get; set; }
    public string? ProductId { get; set; }

    /// <summary>
    /// 图像数据
    /// </summary>
    public object? ImageData { get; set; }

    /// <summary>
    /// 图像路径（如果保存到文件）
    /// </summary>
    public string? ImagePath { get; set; }

    /// <summary>
    /// 曝光时间
    /// </summary>
    public double ExposureTime { get; set; }

    /// <summary>
    /// 采集耗时(ms)
    /// </summary>
    public double AcquisitionTimeMs { get; set; }

    /// <summary>
    /// 图像尺寸
    /// </summary>
    public (int Width, int Height) ImageSize { get; set; }
}

#endregion

#region Flow执行事件

/// <summary>
/// Flow开始执行事件
/// </summary>
public class FlowStartedEvent : IFlowEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
    public string Source { get; set; } = string.Empty; // FlowInstance ID
    public string? RecipeId { get; set; }
    public string? ProductId { get; set; }

    public string FlowDefinitionName { get; set; } = string.Empty;
    public string TriggerSource { get; set; } = string.Empty; // 触发来源
}

/// <summary>
/// Flow节点执行完成事件
/// </summary>
public class FlowNodeCompletedEvent : IFlowEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
    public string Source { get; set; } = string.Empty; // Node ID
    public string? RecipeId { get; set; }
    public string? ProductId { get; set; }

    public string FlowInstanceId { get; set; } = string.Empty;
    public string NodeType { get; set; } = string.Empty;
    public bool IsSuccess { get; set; }
    public double ExecutionTimeMs { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Flow执行完成事件
/// </summary>
public class FlowCompletedEvent : IFlowEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
    public string Source { get; set; } = string.Empty; // FlowInstance ID
    public string? RecipeId { get; set; }
    public string? ProductId { get; set; }

    public string FlowDefinitionName { get; set; } = string.Empty;
    public bool IsSuccess { get; set; }
    public double TotalExecutionTimeMs { get; set; }
    public string? ErrorMessage { get; set; }
}

#endregion

#region 产品相关事件

/// <summary>
/// 产品进入事件
/// </summary>
public class ProductEnterEvent : IFlowEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
    public string Source { get; set; } = string.Empty; // 轨道/工位ID
    public string? RecipeId { get; set; }
    public string ProductId { get; set; } = string.Empty;

    /// <summary>
    /// 进入的工位ID
    /// </summary>
    public string StationId { get; set; } = string.Empty;

    /// <summary>
    /// 条码信息
    /// </summary>
    public string? Barcode { get; set; }

    /// <summary>
    /// 产品类型
    /// </summary>
    public string? ProductType { get; set; }
}

/// <summary>
/// 产品离开事件
/// </summary>
public class ProductLeaveEvent : IFlowEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
    public string Source { get; set; } = string.Empty; // 轨道/工位ID
    public string? RecipeId { get; set; }
    public string ProductId { get; set; } = string.Empty;

    public string StationId { get; set; } = string.Empty;
    public bool InspectionPassed { get; set; }
    public string? NextStationId { get; set; }
}

#endregion
