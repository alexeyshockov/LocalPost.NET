using LocalPost.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace LocalPost;

internal readonly record struct RegistrationContext(IServiceCollection Services, AssistedService Target);

// TODO Make internal
internal delegate Task StreamProcessor<in T>(IAsyncEnumerable<T> stream, CancellationToken ct);

internal delegate IAsyncEnumerable<T> PipelineFactory<out T>(IServiceProvider provider);

internal delegate void PipelineRegistration<in T>(RegistrationContext services, PipelineFactory<T> source);



internal delegate IAsyncEnumerable<TOut> PipelineMiddleware<in TIn, out TOut>(IAsyncEnumerable<TIn> source,
    CancellationToken ct = default);



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

// Too narrow use case
// public interface IHandlerMiddleware<in TIn, out TOut>
// {
//     Handler<TIn> Invoke(Handler<TOut> next);
// }
