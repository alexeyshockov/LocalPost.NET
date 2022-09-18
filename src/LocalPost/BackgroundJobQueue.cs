using System.Threading.Channels;

namespace LocalPost;

public delegate Task Job(CancellationToken ct);

public interface IBackgroundJobQueue : IBackgroundQueue<Job>
{
}

internal sealed class BackgroundJobQueue : IBackgroundJobQueue, IBackgroundQueueReader<Job>
{
    private readonly Channel<Job> _messages = Channel.CreateUnbounded<Job>(new UnboundedChannelOptions
    {
        SingleReader = false,
        SingleWriter = false,
    });

    public ValueTask Enqueue(Job item, CancellationToken ct) => _messages.Writer.WriteAsync(item, ct);

    public ChannelReader<Job> Reader => _messages.Reader;
}
