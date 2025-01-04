namespace LocalPost;

[PublicAPI]
public static class HandlerStackOps
{
    // Just resolve it manually, it's one line longer, same cognitive load or even less, and one additional type less
    // public static HandlerFactory<TIn> Map<TIn, TOut>(this HandlerFactory<TOut> hf,
    //     HandlerMiddlewareFactory<TIn, TOut> middlewareFactory) => provider =>
    // {
    //     var h = hf(provider);
    //     var m = middlewareFactory(provider);
    //
    //     return m(h);
    // };

    // Too narrow use case, and makes Map() inconvenient to use
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
    //     where T : IHandlerMiddleware<TIn, TOut> => hf.Map<TIn, TOut>(provider =>
    //     ActivatorUtilities.CreateInstance<T>(provider).Invoke);

    public static HandlerFactory<TIn> Map<TIn, TOut>(this HandlerFactory<TOut> hf,
        HandlerMiddleware<TIn, TOut> middleware) => provider =>
    {
        var handler = hf(provider);
        return middleware(handler);
    };

    public static HandlerFactory<T> Touch<T>(this HandlerFactory<T> hf,
        HandlerMiddleware<T, T> middleware) => hf.Map(middleware);

    public static HandlerFactory<T> Dispose<T>(this HandlerFactory<T> hf) where T : IDisposable =>
        hf.Touch(next => async (context, ct) =>
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
        hf.Touch(next => async (context, ct) =>
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
        hf.Touch(next => async (context, ct) =>
        {
            if (pred(context))
                return;

            await next(context, ct);
        });
}
