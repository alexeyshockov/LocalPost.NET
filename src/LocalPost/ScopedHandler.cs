using Microsoft.Extensions.DependencyInjection;

namespace LocalPost;

internal static class ScopedHandler
{
    public static HandlerFactory<T> Wrap<T>(HandlerFactory<T> handlerFactory) => provider =>
    {
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        return new ScopedHandler<T>(scopeFactory, handlerFactory).InvokeAsync;
    };

}

internal sealed class ScopedHandler<T> : IHandler<T>
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly HandlerFactory<T> _handlerFactory;

    public ScopedHandler(IServiceScopeFactory scopeFactory, HandlerFactory<T> handlerFactory)
    {
        _scopeFactory = scopeFactory;
        _handlerFactory = handlerFactory;
    }

    public async ValueTask InvokeAsync(T payload, CancellationToken ct)
    {
        // See https://andrewlock.net/exploring-dotnet-6-part-10-new-dependency-injection-features-in-dotnet-6/#handling-iasyncdisposable-services-with-iservicescope
        // And also https://devblogs.microsoft.com/dotnet/announcing-net-6/#microsoft-extensions-dependencyinjection-createasyncscope-apis
        await using var scope = _scopeFactory.CreateAsyncScope();

        var handler = _handlerFactory(scope.ServiceProvider);

        await handler(payload, ct);
    }
}
