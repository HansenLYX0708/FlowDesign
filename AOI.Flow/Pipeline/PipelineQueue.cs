using System.Threading.Channels;

namespace AOI.Flow.Pipeline;

public class PipelineQueue
{
    private readonly Channel<object> _queue
        = Channel.CreateUnbounded<object>();

    public async Task EnqueueAsync(object item)
    {
        await _queue.Writer.WriteAsync(item);
    }

    public async Task<object> DequeueAsync()
    {
        return await _queue.Reader.ReadAsync();
    }
}