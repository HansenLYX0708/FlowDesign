namespace AOI.Flow.Node;

/// <summary>
/// 节点执行结果
/// </summary>
public class NodeResult
{
    public bool Success { get; set; }

    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 结果状态
    /// </summary>
    public NodeResultStatus Status { get; set; } = NodeResultStatus.Success;

    /// <summary>
    /// 执行时长（毫秒）
    /// </summary>
    public double ExecutionTimeMs { get; set; }

    /// <summary>
    /// 重试次数
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// 输出数据
    /// </summary>
    public Dictionary<string, object> OutputData { get; set; } = new();

    /// <summary>
    /// 是否被跳过
    /// </summary>
    public bool IsSkipped => Status == NodeResultStatus.Skipped;

    public static NodeResult Ok() => new() { Success = true, Status = NodeResultStatus.Success };

    public static NodeResult Fail(string msg) => new() { Success = false, Status = NodeResultStatus.Failed, ErrorMessage = msg };

    public static NodeResult Skipped(string? reason = null) => new()
    {
        Success = true,
        Status = NodeResultStatus.Skipped,
        ErrorMessage = reason ?? "Skipped by condition"
    };

    public static NodeResult FromException(Exception ex) => new()
    {
        Success = false,
        Status = NodeResultStatus.Failed,
        ErrorMessage = ex.Message
    };

    public NodeResult WithData(string key, object value)
    {
        OutputData[key] = value;
        return this;
    }

    public NodeResult WithExecutionTime(double ms)
    {
        ExecutionTimeMs = ms;
        return this;
    }
}

/// <summary>
/// 节点结果状态
/// </summary>
public enum NodeResultStatus
{
    Success,    // 成功
    Failed,     // 失败
    Skipped,    // 被跳过
    Cancelled,  // 被取消
    Timeout     // 超时
}