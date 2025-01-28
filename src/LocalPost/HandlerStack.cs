using Microsoft.Extensions.DependencyInjection;

namespace LocalPost;

// [PublicAPI]
// public static class HandlerStack<T>
// {
//     public static readonly HandlerFactory<T> Empty = _ => (_, _) => default;
// }

// [PublicAPI]
// public static class HandlerStack
// {
//     public static HandlerFactory<T> For<T>(Action<T> syncHandler) => For<T>((payload, _) =>
//     {
//         syncHandler(payload);
//         return ValueTask.CompletedTask;
//     });
//
//     public static HandlerFactory<T> For<T>(Handler<T> handler) => _ => handler;
//
//     public static HandlerFactory<T> From<THandler, T>() where THandler : IHandler<T> =>
//         provider => provider.GetRequiredService<THandler>().InvokeAsync;
// }

[PublicAPI]
public static class HandlerStack
{
    public static HandlerManagerFactory<T> For<T>(Action<T> syncHandler) => For<T>((payload, _) =>
    {
        syncHandler(payload);
        return ValueTask.CompletedTask;
    });

    public static HandlerManagerFactory<T> For<T>(Handler<T> handler) => _ => new HandlerManager<T>(handler);

    public static HandlerManagerFactory<T> From<THandler, T>() where THandler : IHandler<T> =>
        provider => new HandlerManager<T>(provider.GetRequiredService<THandler>().InvokeAsync);
}
