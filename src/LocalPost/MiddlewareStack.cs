using System.Collections.Immutable;

namespace LocalPost;

public sealed class MiddlewareStack<T>
{
    private readonly HandlerFactory<T> _handlerFactory;
    private readonly ImmutableArray<MiddlewareFactory<T>> _middlewares;

    public MiddlewareStack(HandlerFactory<T> handlerFactory, IEnumerable<MiddlewareFactory<T>>? middlewares = null)
    {
        _handlerFactory = handlerFactory;
        _middlewares = middlewares?.ToImmutableArray() ?? ImmutableArray<MiddlewareFactory<T>>.Empty;
    }

    public Handler<T> Resolve(IServiceProvider provider)
    {
        var middlewares = _middlewares.Select(factory => factory(provider));

        var handler = _handlerFactory(provider);
        foreach (var middleware in middlewares) // TODO Reverse?
            handler = middleware(handler);

        return handler;
    }
}
