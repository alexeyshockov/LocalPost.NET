using System.Threading.Channels;
using Microsoft.Extensions.Options;

namespace LocalPost;

public interface IBackgroundQueue<in T>
{
    // TODO Custom exception when closed?.. Or just return true/false?..
    ValueTask Enqueue(T item, CancellationToken ct = default);
}

// TODO Open to public later
internal interface IBackgroundQueueManager<in T>
{
    // Implement later for a better health check
//    bool IsFull { get; }

    bool IsClosed { get; }

    ValueTask CompleteAsync(CancellationToken ct = default);
}

public interface IHandler<in TOut>
{
    Task InvokeAsync(TOut payload, CancellationToken ct);
}

public interface IMiddleware<T>
{
    Handler<T> Invoke(Handler<T> next);
}

public delegate Task Handler<in T>(T context, CancellationToken ct);

public delegate Handler<T> HandlerFactory<in T>(IServiceProvider provider);

public delegate Handler<T> Middleware<T>(Handler<T> next);
//public delegate Task Middleware<T>(T context, Handler<T> next, CancellationToken ct);

public delegate Middleware<T> MiddlewareFactory<T>(IServiceProvider provider);



public sealed partial class BackgroundQueue<T> : IBackgroundQueue<T>, IBackgroundQueueManager<T>, IAsyncEnumerable<T>
{
    private readonly TimeSpan _completionTimeout;

    // For the DI container
    public BackgroundQueue(IOptions<BackgroundQueueOptions<T>> options) : this(options.Value.Queue)
    {
    }

    public BackgroundQueue(QueueOptions options) : this(
        options.MaxSize switch
        {
            not null => Channel.CreateBounded<T>(new BoundedChannelOptions(options.MaxSize.Value)
            {
                SingleReader = true,
                SingleWriter = false,
            }),
            _ => Channel.CreateUnbounded<T>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false,
            })
        },
        TimeSpan.FromMilliseconds(options.CompletionTimeout ?? 0))
    {
    }

    public BackgroundQueue(Channel<T> messages, TimeSpan completionTimeout)
    {
        _completionTimeout = completionTimeout;
        Messages = messages;
    }

    protected Channel<T> Messages { get; }

    public bool IsClosed { get; private set; }

    public ValueTask Enqueue(T item, CancellationToken ct = default) => Messages.Writer.WriteAsync(item, ct);

    public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken ct = default)
    {
        while (await Messages.Reader.WaitToReadAsync(ct))
            while (Messages.Reader.TryRead(out var item))
                yield return item;
    }

    public async ValueTask CompleteAsync(CancellationToken ct = default)
    {
        if (IsClosed)
            return;

        await Task.Delay(_completionTimeout, ct);

        Messages.Writer.Complete(); // TODO Handle exceptions
        IsClosed = true;
    }
}
