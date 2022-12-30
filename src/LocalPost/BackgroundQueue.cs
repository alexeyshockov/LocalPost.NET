using System.Threading.Channels;

namespace LocalPost;

public interface IBackgroundQueue<in T>
{
    ValueTask Enqueue(T item, CancellationToken ct = default);
}

public interface IBackgroundQueueReader<TOut>
{
    public ChannelReader<TOut> Reader { get; }
}

public interface IMessageHandler<in TOut>
{
    Task Process(TOut payload, CancellationToken ct);
}

public delegate Task MessageHandler<in T>(T context, CancellationToken ct);



// Simplest background queue
public sealed class BackgroundQueue<T> : IBackgroundQueue<T>, IAsyncEnumerable<T>
{
    private readonly Channel<T> _messages = Channel.CreateUnbounded<T>(new UnboundedChannelOptions
    {
        SingleReader = false,
        SingleWriter = false,
    });

    public ValueTask Enqueue(T item, CancellationToken ct = default) => _messages.Writer.WriteAsync(item, ct);

    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken ct = default) =>
        _messages.Reader.ReadAllAsync(ct).GetAsyncEnumerator(ct);
}
