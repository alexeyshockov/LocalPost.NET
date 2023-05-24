using System.Threading.Channels;
using Microsoft.Extensions.Options;

namespace LocalPost;

public partial interface IBackgroundQueue<in T>
{
    // TODO Custom exception when closed?.. Or just return true/false?..
    ValueTask Enqueue(T item, CancellationToken ct = default);
}

// TODO Open to public later
internal interface IBackgroundQueueManager
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



internal sealed partial class BackgroundQueue<T> : IBackgroundQueue<T>, IBackgroundQueueManager, IAsyncEnumerable<T>
{
    private readonly TimeSpan _completionTimeout;

    public BackgroundQueue(BackgroundQueueOptions options) : this(
        options.MaxSize switch
        {
            not null => Channel.CreateBounded<T>(new BoundedChannelOptions(options.MaxSize.Value)
            {
                SingleReader = options.MaxConcurrency == 1,
                SingleWriter = false,
            }),
            _ => Channel.CreateUnbounded<T>(new UnboundedChannelOptions
            {
                SingleReader = options.MaxConcurrency == 1,
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

    internal Channel<T> Messages { get; }

    public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken ct = default)
    {
        // Track full or not later
        while (await Messages.Reader.WaitToReadAsync(ct))
            while (Messages.Reader.TryRead(out var item))
                yield return item;
    }

    public static implicit operator ChannelReader<T>(BackgroundQueue<T> that) => that.Messages.Reader;

    public static implicit operator ChannelWriter<T>(BackgroundQueue<T> that) => that.Messages.Writer;

    public ValueTask Enqueue(T item, CancellationToken ct = default) => Messages.Writer.WriteAsync(item, ct);

    public bool IsClosed { get; private set; }

    public async ValueTask CompleteAsync(CancellationToken ct = default)
    {
        if (IsClosed)
            return;

        await Task.Delay(_completionTimeout, ct);

        Messages.Writer.Complete(); // TODO Handle exceptions
        IsClosed = true;
    }
}
