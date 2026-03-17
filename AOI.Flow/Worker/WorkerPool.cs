using AOI.Flow.Pipeline;

namespace AOI.Flow.Worker;

public class WorkerPool
{
    private readonly int _workerCount;

    private readonly Func<object, Task> _worker;

    public WorkerPool(int workerCount,
                      Func<object, Task> worker)
    {
        _workerCount = workerCount;
        _worker = worker;
    }

    public void Start(PipelineQueue queue,
                      CancellationToken token)
    {
        for (int i = 0; i < _workerCount; i++)
        {
            Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    var item = await queue.DequeueAsync();

                    await _worker(item);
                }

            }, token);
        }
    }
}