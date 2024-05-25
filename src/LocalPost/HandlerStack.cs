using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;

namespace LocalPost;

[PublicAPI]
public static class HandlerStack<T>
{
    public static readonly HandlerFactory<T> Empty = _ => (_, _) => default;
}

[PublicAPI]
public static class HandlerStack
{
    public static HandlerFactory<T> For<T>(Handler<T> handler) => _ => handler;

    public static HandlerFactory<T> From<THandler, T>() where THandler : IHandler<T> =>
        provider => provider.GetRequiredService<THandler>().InvokeAsync;
}
