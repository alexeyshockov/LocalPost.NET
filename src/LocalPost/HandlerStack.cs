using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;

namespace LocalPost;



public interface IHandler<in TOut>
{
    ValueTask InvokeAsync(TOut payload, CancellationToken ct);
}

//public interface IMiddleware<T>
//{
//    Handler<T> Invoke(Handler<T> next);
//}

public delegate ValueTask Handler<in T>(T context, CancellationToken ct);

public delegate Handler<T> HandlerFactory<in T>(IServiceProvider provider);

//public delegate Handler<T> Middleware<T>(Handler<T> next);
////public delegate Task Middleware<T>(T context, Handler<T> next, CancellationToken ct);
//
//public delegate Middleware<T> MiddlewareFactory<T>(IServiceProvider provider);



public delegate Handler<TIn> HandlerMiddleware<in TIn, out TOut>(Handler<TOut> next);
//public delegate Handler<T> HandlerMiddleware<T>(Handler<T> next);

public delegate HandlerMiddleware<TIn, TOut> HandlerMiddlewareFactory<in TIn, out TOut>(IServiceProvider provider);
//public delegate HandlerMiddleware<T, T> HandlerMiddlewareFactory<T>(IServiceProvider provider);


public delegate HandlerFactory<TIn> HandlerFactoryMiddleware<in TIn, out TOut>(HandlerFactory<TOut> hf);
//public delegate HandlerFactory<T> HandlerFactoryMiddleware<T>(HandlerFactory<T> hf);

public interface IHandlerMiddleware<in TIn, out TOut>
{
    Handler<TIn> Invoke(Handler<TOut> next);
}
//public interface IHandlerMiddleware<T> : IMiddleware2<T, T>
//{
//}

[PublicAPI]
public static partial class HandlerStack
{
    public static HandlerFactory<T> For<T>(Handler<T> handler) => _ => handler;

    public static HandlerFactory<T> From<THandler, T>() where THandler : IHandler<T> =>
        provider => provider.GetRequiredService<THandler>().InvokeAsync;



    public static HandlerFactory<TIn> Map<TIn, TOut>(this HandlerFactory<TOut> handlerFactory,
        HandlerFactoryMiddleware<TIn, TOut> middleware) => middleware(handlerFactory);

    public static HandlerFactory<TIn> Map<TIn, TOut>(this HandlerFactory<TOut> handlerFactory,
        HandlerMiddlewareFactory<TIn, TOut> middlewareFactory) => provider =>
    {
        var h = handlerFactory(provider);
        var m = middlewareFactory(provider);

        return m(h);
    };

//    public static HandlerFactory<T> Map<T>(this HandlerFactory<T> handlerFactory,
//        HandlerMiddlewareFactory<T> middlewareFactory) => provider =>
//    {
//        var h = handlerFactory(provider);
//        var m = middlewareFactory(provider);
//
//        return m(h);
//    };

    public static HandlerFactory<TIn> Map<TIn, TOut>(this HandlerFactory<TOut> handlerFactory,
        HandlerMiddleware<TIn, TOut> middleware) => handlerFactory.Map(_ => middleware);

//    public static HandlerFactory<T> Map<T>(this HandlerFactory<T> handlerFactory,
//        HandlerMiddleware<T> middleware) => handlerFactory.Map(_ => middleware);

    // Really no need...
//    public static HandlerFactory<TIn> Map<TIn, TOut>(this HandlerFactory<TOut> handlerFactory,
//        IMiddleware2<TIn, TOut> middleware) => handlerFactory.Map(middleware.Invoke);

    public static HandlerFactory<T> Scoped<T>(this HandlerFactory<T> hf) => hf.Map(ScopedHandler.Wrap);

    public static HandlerFactory<T> SkipWhen<T>(this HandlerFactory<T> handlerStack, Func<T, bool> pred) =>
        handlerStack.Map<T, T>(next => async (context, ct) =>
        {
            if (pred(context))
                return;

            await next(context, ct);
        });


//    public static HandlerFactory<TIn> Append<TIn, TOut>(this HandlerFactory<TOut> handlerFactory,
//        HandlerMiddlewareFactory<TIn, TOut> handlerMiddlewareFactory) =>
//        provider => handlerMiddlewareFactory(provider)(handlerFactory(provider));
//
//    public static HandlerFactory<T> Append<T>(this HandlerFactory<T> handlerFactory,
//        HandlerMiddlewareFactory<T> handlerMiddlewareFactory) =>
//        provider => handlerMiddlewareFactory(provider)(handlerFactory(provider));
//
//    // Because...
////    public static HandlerFactory<TIn> Append<TIn, TOut>(this HandlerFactory<TOut> handlerFactory,
////        Func<IServiceProvider, Middleware2<TIn, TOut>> middlewareFactory) =>
////        handlerFactory.Append<TIn, TOut>(provider => middlewareFactory(provider));
//
//    public static HandlerFactory<TIn> AppendMiddleware<TIn, TOut>(this HandlerFactory<TOut> handlerFactory,
//        HandlerMiddleware<TIn, TOut> middleware) =>
//        handlerFactory.Append(_ => middleware);
//
//    public static HandlerFactory<TIn> Append<TIn, TOut>(this HandlerFactory<TOut> handlerFactory,
//        IMiddleware2<TIn, TOut> middleware) =>
//        handlerFactory.Append<TIn, TOut>(_ => middleware.Invoke);
//
//    // C# can only infer ALL generics of a method... Or nothing, so you have to specify each one manually. Not very
//    // convenient, but creating a wrapper class is even worse.
//    public static HandlerFactory<TIn> Append<TMiddleware, TIn, TOut>(this HandlerFactory<TOut> handlerFactory)
//        where TMiddleware : class, IMiddleware2<TIn, TOut> =>
//        handlerFactory.Append<TIn, TOut>(provider => provider.GetRequiredService<TMiddleware>().Invoke);
//
//    // C# can only infer ALL generics of a method... Or nothing, so you have to specify each one manually. Not very
//    // convenient, but creating a wrapper class is even worse.
//    public static HandlerFactory<T> Append<TMiddleware, T>(this HandlerFactory<T> handlerFactory)
//        where TMiddleware : class, IMiddleware2<T, T> => handlerFactory.Append<TMiddleware, T, T>();
}

[PublicAPI]
public static class HandlerStack<T>
{
    public static readonly HandlerFactory<T> Empty = _ => (_, _) => default;

//    public static HandlerStack2<T> From(Handler<T> handler) => new()
//    {
//        HandlerFactory = _ => handler
//    };
//
//    public static HandlerStack2<T> From<THandler>() where THandler : IHandler<T> => new()
//    {
//        HandlerFactory = provider => provider.GetRequiredService<THandler>().InvokeAsync
//    };
//
//    public required HandlerFactory<T> HandlerFactory { get; init; }
//
//    public static implicit operator HandlerFactory<T>(HandlerStack2<T> stack) => stack.HandlerFactory;
//
//    public HandlerStack2<TIn> Append<TIn>(MiddlewareFactory2<TIn, T> middlewareFactory) => new()
//    {
//        HandlerFactory = provider => middlewareFactory(provider)(HandlerFactory(provider))
//    };
//
//    public HandlerStack2<TIn> Append<TIn>(Middleware2<TIn, T> middleware) => Append(_ => middleware);
//
//    public HandlerStack2<T> Append<TMiddleware>() where TMiddleware : class, IMiddleware2<T, T> =>
//        Append<TMiddleware, T>();
//
//    public HandlerStack2<TIn> Append<TMiddleware, TIn>() where TMiddleware : class, IMiddleware2<TIn, T> =>
//        Append<TIn>(provider => provider.GetRequiredService<TMiddleware>().Invoke);
//
//    public HandlerStack2<T> Scoped() => new()
//    {
//        HandlerFactory = ScopedHandlerFactory.Wrap(HandlerFactory)
//    };
}






// TODO Remove
//[PublicAPI]
//public sealed class HandlerStackBuilder<T> : HandlerStackBuilder<T, HandlerStackBuilder<T>>
//{
//}
//
//[PublicAPI]
//public abstract class HandlerStackBuilder<T, TBuilder>
//    where TBuilder : HandlerStackBuilder<T, TBuilder>
//{
//    protected readonly List<MiddlewareFactory<T>> Middlewares = new();
//    protected HandlerFactory<T> HandlerFactory = _ => (_, _) => Task.CompletedTask;
//
//    public TBuilder SetHandler(Handler<T> handler) => SetHandler(_ => handler);
//
//    public TBuilder SetHandler<THandler>() where THandler : IHandler<T> =>
//        SetHandler(provider => provider.GetRequiredService<THandler>().InvokeAsync);
//
//    public TBuilder SetHandler(HandlerFactory<T> factory)
//    {
//        HandlerFactory = factory;
//
//        return (TBuilder) this;
//    }
//
////    public TBuilder Append<TMiddleware>() where TMiddleware : IHandler<T>
////    {
////        Middlewares.Add(provider => next => ActivatorUtilities.CreateInstance<TMiddleware>(provider, next).InvokeAsync);
////
////        return (TBuilder) this;
////    }
//
//    public TBuilder Append(Middleware<T> middleware) =>
//        Append(_ => middleware);
//
//    public TBuilder Append<TMiddleware>() where TMiddleware : class, IMiddleware<T>
//    {
//        Middlewares.Add(provider => provider.GetRequiredService<TMiddleware>().Invoke);
//
//        return (TBuilder) this;
//    }
//
//    public TBuilder Append(MiddlewareFactory<T> factory)
//    {
//        Middlewares.Add(factory);
//
//        return (TBuilder) this;
//    }
//
//    internal HandlerStack<T> Build() => new(HandlerFactory, Middlewares);
//}
//
//[PublicAPI]
//public sealed class HandlerStack<T>
//{
//    private readonly HandlerFactory<T> _handler;
//    private readonly ImmutableArray<MiddlewareFactory<T>> _middlewares;
//
//    public HandlerStack(HandlerFactory<T> handler, IEnumerable<MiddlewareFactory<T>>? middlewares = null)
//    {
//        _handler = handler;
//        _middlewares = middlewares?.ToImmutableArray() ?? ImmutableArray<MiddlewareFactory<T>>.Empty;
//    }
//
//    public Handler<T> Resolve(IServiceProvider provider) => _middlewares
//        .Select(factory => factory(provider))
//        .Reverse()
//        .Aggregate(_handler(provider), (next, middleware) => middleware(next));
//}
