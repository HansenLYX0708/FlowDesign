using AOI.Device.Manager;
using AOI.Flow.Pipeline;

namespace AOI.Flow.Model;

public class FlowContext
{
    public Dictionary<string, object> Data { get; } = new();

    public DeviceManager DeviceManager { get; }

    public PipelineQueue Pipeline { get; }

    public CancellationToken Token { get; }

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
}