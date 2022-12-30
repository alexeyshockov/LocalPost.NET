using System.Threading.Channels;

namespace LocalPost;

public delegate Task Job(CancellationToken ct);

public interface IBackgroundJobQueue : IBackgroundQueue<Job>
{
}

internal sealed class BackgroundJobQueue : IBackgroundJobQueue, IAsyncEnumerable<Job>
{
    private readonly Channel<Job> _messages = Channel.CreateUnbounded<Job>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false,
    });

    public ValueTask Enqueue(Job item, CancellationToken ct = default) => _messages.Writer.WriteAsync(item, ct);

    public IAsyncEnumerator<Job> GetAsyncEnumerator(CancellationToken cancellationToken = default) =>
        _messages.Reader.ReadAllAsync(cancellationToken).GetAsyncEnumerator(cancellationToken);
}
