namespace AOI.Flow.Recipe;

/// <summary>
/// 工业级Recipe（配方）定义 - 产品流程参数配置
/// </summary>
public class Recipe
{
    /// <summary>
    /// Recipe唯一标识
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Recipe名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 产品型号/料号
    /// </summary>
    public string ProductCode { get; set; } = string.Empty;

    /// <summary>
    /// 产品描述
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 客户代码
    /// </summary>
    public string? CustomerCode { get; set; }

    /// <summary>
    /// 版本号（用于版本控制）
    /// </summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 最后修改时间
    /// </summary>
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 创建者
    /// </summary>
    public string? CreatedBy { get; set; }

    /// <summary>
    /// Recipe状态
    /// </summary>
    public RecipeStatus Status { get; set; } = RecipeStatus.Draft;

    /// <summary>
    /// 关联的Flow定义名称
    /// </summary>
    public string FlowDefinitionName { get; set; } = string.Empty;

    /// <summary>
    /// 节点参数集合（Key: NodeId, Value: 节点参数）
    /// </summary>
    public Dictionary<string, NodeParameters> NodeParameters { get; set; } = new();

    /// <summary>
    /// 全局参数（跨节点共享的参数）
    /// </summary>
    public Dictionary<string, ParameterValue> GlobalParameters { get; set; } = new();

    /// <summary>
    /// 检测规格参数
    /// </summary>
    public InspectionSpecs InspectionSpecs { get; set; } = new();

    /// <summary>
    /// 设备配置参数
    /// </summary>
    public DeviceConfigs DeviceConfigs { get; set; } = new();

    /// <summary>
    /// 产品尺寸参数
    /// </summary>
    public ProductDimensions Dimensions { get; set; } = new();

    /// <summary>
    /// 自定义扩展参数
    /// </summary>
    public Dictionary<string, object> Extensions { get; set; } = new();

    /// <summary>
    /// 父Recipe ID（用于继承/派生）
    /// </summary>
    public string? ParentRecipeId { get; set; }

    /// <summary>
    /// 标签（用于分类和搜索）
    /// </summary>
    public List<string> Tags { get; set; } = new();
}

public enum RecipeStatus
{
    Draft,      // 草稿
    Active,     // 激活（可生产使用）
    Archived,   // 归档（历史版本）
    Deprecated  // 废弃
}

/// <summary>
/// 节点参数集合
/// </summary>
public class NodeParameters
{
    /// <summary>
    /// 节点类型
    /// </summary>
    public string NodeType { get; set; } = string.Empty;

    /// <summary>
    /// 是否启用该节点
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 节点配置参数
    /// </summary>
    public Dictionary<string, ParameterValue> Config { get; set; } = new();

    /// <summary>
    /// 执行超时时间(ms)
    /// </summary>
    public int TimeoutMs { get; set; } = 10000;

    /// <summary>
    /// 重试次数
    /// </summary>
    public int RetryCount { get; set; } = 0;

    /// <summary>
    /// 重试间隔(ms)
    /// </summary>
    public int RetryIntervalMs { get; set; } = 1000;
}

/// <summary>
/// 参数值 - 支持多种数据类型
/// </summary>
public class ParameterValue
{
    public ParameterType Type { get; set; }
    public object? Value { get; set; }
    public string? Unit { get; set; } // 单位（如mm, ms, px等）
    public string? Description { get; set; }
    public ParameterRange? Range { get; set; } // 有效范围

    // 便捷转换方法
    public int AsInt() => Convert.ToInt32(Value);
    public double AsDouble() => Convert.ToDouble(Value);
    public bool AsBool() => Convert.ToBoolean(Value);
    public string AsString() => Value?.ToString() ?? string.Empty;
    public T? As<T>() => Value is T t ? t : default;
}

public enum ParameterType
{
    Integer,
    Double,
    Boolean,
    String,
    Enum,
    Point2D,      // (X, Y)
    Point3D,      // (X, Y, Z)
    Rectangle,    // (X, Y, W, H)
    Region,       // 不规则区域
    Color,        // 颜色值
    Array,        // 数组
    Object        // 复杂对象
}

/// <summary>
/// 参数有效范围
/// </summary>
public class ParameterRange
{
    public double Min { get; set; }
    public double Max { get; set; }
    public double? Step { get; set; }
    public List<object>? AllowedValues { get; set; } // 枚举值列表
}

/// <summary>
/// 检测规格
/// </summary>
public class InspectionSpecs
{
    /// <summary>
    /// 检测项列表
    /// </summary>
    public List<InspectionItem> Items { get; set; } = new();

    /// <summary>
    /// 整体通过标准
    /// </summary>
    public PassCriteria Criteria { get; set; } = new();

    /// <summary>
    /// 图像质量要求
    /// </summary>
    public ImageQualityRequirements ImageQuality { get; set; } = new();
}

public class InspectionItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public InspectionType Type { get; set; }
    public bool IsEnabled { get; set; } = true;
    public double MinValue { get; set; }
    public double MaxValue { get; set; }
    public double? NominalValue { get; set; }
    public double Tolerance { get; set; }
    public string? Unit { get; set; }
    public SeverityLevel Severity { get; set; } = SeverityLevel.Error;
    public RegionOfInterest? ROI { get; set; }
    public Dictionary<string, ParameterValue> AlgorithmParams { get; set; } = new();
}

public enum InspectionType
{
    Dimension,      // 尺寸测量
    Presence,       // 存在/缺失检测
    Position,       // 位置检测
    Orientation,    // 方向检测
    Surface,        // 表面检测
    Color,          // 颜色检测
    Barcode,        // 条码/二维码识别
    OCR,            // 字符识别
    Defect,         // 缺陷检测
    Custom          // 自定义算法
}

public enum SeverityLevel
{
    Info,       // 信息
    Warning,    // 警告
    Error,      // 错误
    Critical    // 致命
}

public class PassCriteria
{
    /// <summary>
    /// 最大允许缺陷数
    /// </summary>
    public int MaxDefectCount { get; set; } = int.MaxValue;

    /// <summary>
    /// 最大允许致命缺陷数
    /// </summary>
    public int MaxCriticalDefects { get; set; } = 0;

    /// <summary>
    /// 关键检测项必须全部通过
    /// </summary>
    public bool RequireCriticalItemsPass { get; set; } = true;
}

public class ImageQualityRequirements
{
    public int MinResolutionX { get; set; }
    public int MinResolutionY { get; set; }
    public double MinContrast { get; set; }
    public double MaxNoise { get; set; }
    public double MinSharpness { get; set; }
}

public class RegionOfInterest
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public double Rotation { get; set; }
    public string? Name { get; set; }
}

/// <summary>
/// 设备配置
/// </summary>
public class DeviceConfigs
{
    /// <summary>
    /// 相机配置
    /// </summary>
    public Dictionary<string, CameraConfig> Cameras { get; set; } = new();

    /// <summary>
    /// 光源配置
    /// </summary>
    public Dictionary<string, LightConfig> Lights { get; set; } = new();

    /// <summary>
    /// 轴配置
    /// </summary>
    public Dictionary<string, AxisConfig> Axes { get; set; } = new();

    /// <summary>
    /// PLC/IO配置
    /// </summary>
    public Dictionary<string, IOConfig> IOs { get; set; } = new();
}

public class CameraConfig
{
    public string DeviceId { get; set; } = string.Empty;
    public double ExposureTime { get; set; } // us
    public double Gain { get; set; }
    public int ROI_X { get; set; }
    public int ROI_Y { get; set; }
    public int ROI_Width { get; set; }
    public int ROI_Height { get; set; }
    public int BinningX { get; set; } = 1;
    public int BinningY { get; set; } = 1;
    public string? PixelFormat { get; set; }
    public double FrameRate { get; set; }
    public Dictionary<string, ParameterValue> CustomParams { get; set; } = new();
}

public class LightConfig
{
    public string DeviceId { get; set; } = string.Empty;
    public int Intensity { get; set; } // 0-255 或 0-100
    public int DurationMs { get; set; }
    public string? Color { get; set; } // Red, Green, Blue, White等
    public LightMode Mode { get; set; } = LightMode.Continuous;
    public int StrobeDelayUs { get; set; }
}

public enum LightMode
{
    Continuous, // 常亮
    Strobe,     // 频闪（与相机同步）
    Triggered   // 外部触发
}

public class AxisConfig
{
    public string DeviceId { get; set; } = string.Empty;
    public double Speed { get; set; }
    public double Acceleration { get; set; }
    public double Deceleration { get; set; }
    public double HomePosition { get; set; }
    public List<PositionPreset> Positions { get; set; } = new();
}

public class PositionPreset
{
    public string Name { get; set; } = string.Empty;
    public double Position { get; set; }
    public double? Speed { get; set; } // null表示使用默认速度
}

public class IOConfig
{
    public string DeviceId { get; set; } = string.Empty;
    public Dictionary<string, bool> InitialStates { get; set; } = new();
    public Dictionary<string, int> PulseDurations { get; set; } = new(); // ms
}

/// <summary>
/// 产品尺寸参数
/// </summary>
public class ProductDimensions
{
    public double Width { get; set; }
    public double Height { get; set; }
    public double Thickness { get; set; }
    public double Weight { get; set; }
    public string? Unit { get; set; } = "mm";
    public Dictionary<string, double> CustomDimensions { get; set; } = new();
}
