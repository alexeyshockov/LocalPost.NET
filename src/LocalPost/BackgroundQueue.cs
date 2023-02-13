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



// Simplest background queue
public sealed partial class BackgroundQueue<T> : IBackgroundQueue<T>, IAsyncEnumerable<T>
{
    // TODO Bounded version (1000 by default), overflow should be dropped with a log message
    private readonly Channel<T> _messages = Channel.CreateUnbounded<T>(new UnboundedChannelOptions
    {
        SingleReader = false,
        SingleWriter = false,
    });

    public ValueTask Enqueue(T item, CancellationToken ct = default) => _messages.Writer.WriteAsync(item, ct);

    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken ct = default) =>
        _messages.Reader.ReadAllAsync(ct).GetAsyncEnumerator(ct);
}
