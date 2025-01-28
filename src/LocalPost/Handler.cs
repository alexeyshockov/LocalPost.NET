namespace LocalPost;

public delegate ValueTask Handler<in T>(T context, CancellationToken ct);

public delegate Handler<T> HandlerFactory<in T>(IServiceProvider provider);

public delegate Handler<TIn> HandlerMiddleware<in TIn, out TOut>(Handler<TOut> next);

// Too narrow use case
// public delegate HandlerMiddleware<TIn, TOut> HandlerMiddlewareFactory<in TIn, out TOut>(IServiceProvider provider);

// Even more narrow use case, confuses more than helps
// public delegate HandlerFactory<TIn> HandlerFactoryMiddleware<in TIn, out TOut>(HandlerFactory<TOut> hf);

public interface IHandler<in TOut>
{
    ValueTask InvokeAsync(TOut payload, CancellationToken ct);
}



public delegate IHandlerManager<T> HandlerManagerFactory<T>(IServiceProvider provider);

public delegate IHandlerManager<TIn> HandlerManagerMiddleware<TIn, TOut>(IHandlerManager<TOut> next);

public interface IHandlerManager<T>
{
    ValueTask<Handler<T>> Start(CancellationToken ct);

    ValueTask Stop(Exception? error, CancellationToken ct);
}

internal sealed class HandlerManager<T>(Handler<T> handler) : IHandlerManager<T>
{
    public ValueTask<Handler<T>> Start(CancellationToken ct) => ValueTask.FromResult(handler);

    public ValueTask Stop(Exception? error, CancellationToken ct) => ValueTask.CompletedTask;
}

internal sealed class HandlerDecorator<TIn, TOut>(
    IHandlerManager<TOut> next, HandlerMiddleware<TIn, TOut> middleware) : IHandlerManager<TIn>
{
    public async ValueTask<Handler<TIn>> Start(CancellationToken ct)
    {
        var nextHandler = await next.Start(ct).ConfigureAwait(false);
        return middleware(nextHandler);
    }

    public ValueTask Stop(Exception? error, CancellationToken ct) => next.Stop(error, ct);
}
