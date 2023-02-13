using Microsoft.Extensions.DependencyInjection;

namespace LocalPost;

public sealed class MiddlewareStackBuilder<T> : MiddlewareStackBuilder<T, MiddlewareStackBuilder<T>>
{
}

public abstract class MiddlewareStackBuilder<T, TBuilder>
    where TBuilder : MiddlewareStackBuilder<T, TBuilder>
{
    protected readonly List<MiddlewareFactory<T>> Middlewares = new();
    protected HandlerFactory<T> HandlerFactory = _ => (c, ct) => Task.CompletedTask;

    public TBuilder SetHandler(Handler<T> handler) => SetHandler(_ => handler);

    public TBuilder SetHandler<THandler>() where THandler : IHandler<T> =>
        SetHandler(provider => provider.GetRequiredService<THandler>().InvokeAsync);

    public TBuilder SetHandler(HandlerFactory<T> factory)
    {
        HandlerFactory = factory;

        return (TBuilder) this;
    }

    //    public TBuilder Append<TMiddleware>() where TMiddleware : IHandler<T>
//    {
//        Middlewares.Add(provider => next => ActivatorUtilities.CreateInstance<TMiddleware>(provider, next).InvokeAsync);
//
//        return (TBuilder) this;
//    }

    public TBuilder Append(Middleware<T> middleware) =>
        Append(_ => middleware);

    public TBuilder Append<TMiddleware>() where TMiddleware : IMiddleware<T>
    {
        Middlewares.Add(provider => provider.GetRequiredService<TMiddleware>().Invoke);

        return (TBuilder) this;
    }

    public TBuilder Append(MiddlewareFactory<T> factory)
    {
        Middlewares.Add(factory);

        return (TBuilder) this;
    }

    internal MiddlewareStack<T> Build() => new(HandlerFactory, Middlewares);
}
