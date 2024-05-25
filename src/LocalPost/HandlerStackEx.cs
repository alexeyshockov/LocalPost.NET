using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LocalPost;

public static partial class HandlerStackEx
{
    // Better use a lambda in place, see Scoped() middleware
    // public static HandlerFactory<TIn> Map<TIn, TOut>(this HandlerFactory<TOut> hf,
    //     HandlerFactoryMiddleware<TIn, TOut> middleware) => middleware(hf);

    // Just resolve it manually, it's one line longer, same cognitive load or even less,
    // and one additional type less
    // public static HandlerFactory<TIn> Map<TIn, TOut>(this HandlerFactory<TOut> hf,
    //     HandlerMiddlewareFactory<TIn, TOut> middlewareFactory) => provider =>
    // {
    //     var h = hf(provider);
    //     var m = middlewareFactory(provider);
    //
    //     return m(h);
    // };

    // Too narrow use case, but makes the Map() method inconvenient to use
    // public static HandlerFactory<TIn> Map<TIn, TOut>(this HandlerFactory<TOut> hf,
    //     Func<IServiceProvider, IHandlerMiddleware<TIn, TOut>> middlewareFactory) => hf.Map<TIn, TOut>(provider =>
    //     middlewareFactory(provider).Invoke);
    // public static HandlerFactory<TIn> Map<TIn, TOut>(this HandlerFactory<TOut> hf,
    //     Func<IServiceProvider, IHandlerMiddleware<TIn, TOut>> middlewareFactory) => provider =>
    // {
    //     var handler = hf(provider);
    //     return middlewareFactory(provider).Invoke(handler);
    // };

    // public static HandlerFactory<TIn> Map<TIn, TOut>(this HandlerFactory<TOut> hf,
    //     HandlerMiddleware<TIn, TOut> middleware) => hf.Map(_ => middleware);
    public static HandlerFactory<TIn> Map<TIn, TOut>(this HandlerFactory<TOut> hf,
        HandlerMiddleware<TIn, TOut> middleware) => provider =>
    {
        var handler = hf(provider);
        return middleware(handler);
    };

    public static HandlerFactory<T> Touch<T>(this HandlerFactory<T> hf,
        HandlerMiddleware<T, T> middleware) => hf.Map(middleware);

    // No need, just use a lambda in place
    // public static HandlerFactory<TIn> Map<TIn, TOut>(this HandlerFactory<TOut> hf,
    //     where T : IHandlerMiddleware<TIn, TOut> => hf.Map<TIn, TOut>(provider =>
    //     ActivatorUtilities.CreateInstance<T>(provider).Invoke);
    //
    // public static HandlerFactory<T> Scoped<T>(this HandlerFactory<T> hf) => hf.Map(ScopedHandler.Wrap);

    public static HandlerFactory<T> Dispose<T>(this HandlerFactory<T> hf) where T : IDisposable =>
        hf.Map<T, T>(next => async (context, ct) =>
        {
            try
            {
                await next(context, ct);
            }
            finally
            {
                context.Dispose();
            }
        });

    public static HandlerFactory<T> DisposeAsync<T>(this HandlerFactory<T> hf) where T : IAsyncDisposable =>
        hf.Map<T, T>(next => async (context, ct) =>
        {
            try
            {
                await next(context, ct);
            }
            finally
            {
                await context.DisposeAsync();
            }
        });

    public static HandlerFactory<T> SkipWhen<T>(this HandlerFactory<T> hf, Func<T, bool> pred) =>
        hf.Map<T, T>(next => async (context, ct) =>
        {
            if (pred(context))
                return;

            await next(context, ct);
        });

    // public static HandlerFactory<T> ShutdownOnError<T>(this HandlerFactory<T> hf, int exitCode = 1) =>
    //     hf.Map<T, T>(provider =>
    //     {
    //         var appLifetime = provider.GetRequiredService<IHostApplicationLifetime>();
    //         return next => async (context, ct) =>
    //         {
    //             try
    //             {
    //                 await next(context, ct);
    //             }
    //             catch (OperationCanceledException e) when (e.CancellationToken == ct)
    //             {
    //                 throw;
    //             }
    //             catch
    //             {
    //                 appLifetime.StopApplication();
    //                 Environment.ExitCode = exitCode;
    //             }
    //         };
    //     });
    public static HandlerFactory<T> ShutdownOnError<T>(this HandlerFactory<T> hf, int exitCode = 1) => provider =>
    {
        var appLifetime = provider.GetRequiredService<IHostApplicationLifetime>();
        var next = hf(provider);

        return async (context, ct) =>
        {
            try
            {
                await next(context, ct);
            }
            catch (OperationCanceledException e) when (e.CancellationToken == ct)
            {
                throw;
            }
            catch
            {
                appLifetime.StopApplication();
                Environment.ExitCode = exitCode;
            }
        };
    };
}
