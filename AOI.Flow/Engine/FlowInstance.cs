using AOI.Flow.DAG;
using AOI.Flow.Model;

namespace AOI.Flow.Engine;

public class FlowInstance
{
    public string Id { get; } = Guid.NewGuid().ToString();

    public FlowDefinition Definition { get; }

    public FlowContext Context { get; }

    public DagRuntimeGraph RuntimeGraph { get; }

    public CancellationTokenSource CancellationTokenSource { get; }

    public Task? ExecutionTask { get; set; }

    public FlowStatus Status { get; private set; } = FlowStatus.Pending;

    public DateTime StartTime { get; private set; }

    public DateTime? EndTime { get; private set; }

    public Exception? Error { get; private set; }

    public FlowInstance(
        FlowDefinition definition,
        FlowContext context,
        DagRuntimeGraph runtimeGraph)
    {
        Definition = definition;
        Context = context;
        RuntimeGraph = runtimeGraph;
        CancellationTokenSource = new CancellationTokenSource();
    }

    public void MarkStarted()
    {
        Status = FlowStatus.Running;
        StartTime = DateTime.UtcNow;
    }

    public void MarkCompleted()
    {
        Status = FlowStatus.Completed;
        EndTime = DateTime.UtcNow;
    }

    public void MarkFailed(Exception error)
    {
        Status = FlowStatus.Failed;
        EndTime = DateTime.UtcNow;
        Error = error;
    }

    public void MarkCancelled()
    {
        Status = FlowStatus.Cancelled;
        EndTime = DateTime.UtcNow;
    }

    public void Cancel()
    {
        CancellationTokenSource.Cancel();
        MarkCancelled();
    }
}

public enum FlowStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled
}