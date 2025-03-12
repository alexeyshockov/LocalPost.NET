namespace LocalPost;


[PublicAPI]
public static partial class HandlerStackOps
{
    public static IHandlerManager<TIn> Map<TIn, TOut>(this IHandlerManager<TOut> hm,
        HandlerMiddleware<TIn, TOut> middleware) => new HandlerDecorator<TIn, TOut>(hm, middleware);

    public static IHandlerManager<T> Touch<T>(this IHandlerManager<T> hm,
        HandlerMiddleware<T, T> middleware) => hm.Map(middleware);

    public static HandlerManagerFactory<TIn> Map<TIn, TOut>(this HandlerManagerFactory<TOut> hmf,
        HandlerManagerMiddleware<TIn, TOut> middleware) => provider =>
    {
        var handler = hmf(provider);
        return middleware(handler);
    };

    public static HandlerManagerFactory<T> Touch<T>(this HandlerManagerFactory<T> hf,
        HandlerManagerMiddleware<T, T> middleware) => hf.Map(middleware);

    public static HandlerManagerFactory<TIn> MapHandler<TIn, TOut>(this HandlerManagerFactory<TOut> hmf,
        HandlerMiddleware<TIn, TOut> middleware) =>
        hmf.Map<TIn, TOut>(next => next.Map(middleware));

    public static HandlerManagerFactory<T> TouchHandler<T>(this HandlerManagerFactory<T> hmf,
        HandlerMiddleware<T, T> middleware) => hmf.MapHandler(middleware);
}
