using Microsoft.Extensions.DependencyInjection;

namespace LocalPost;

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
