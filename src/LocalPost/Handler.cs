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
