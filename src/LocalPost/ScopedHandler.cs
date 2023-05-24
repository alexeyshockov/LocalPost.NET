using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LocalPost;

internal sealed class ScopedHandler<T> : IHandler<T>
{
    private readonly ILogger<ScopedHandler<T>> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly HandlerFactory<T> _handlerFactory;

    private readonly string _name;

    public ScopedHandler(ILogger<ScopedHandler<T>> logger, string name, IServiceScopeFactory scopeFactory,
        HandlerFactory<T> handlerFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _handlerFactory = handlerFactory;
        _name = name;
    }

    public async Task InvokeAsync(T payload, CancellationToken ct)
    {
        // TODO Tracing...

        // See https://andrewlock.net/exploring-dotnet-6-part-10-new-dependency-injection-features-in-dotnet-6/#handling-iasyncdisposable-services-with-iservicescope
        // And also https://devblogs.microsoft.com/dotnet/announcing-net-6/#microsoft-extensions-dependencyinjection-createasyncscope-apis
        await using var scope = _scopeFactory.CreateAsyncScope();

        // Make it specific for this queue somehow?..
        var handler = _handlerFactory(scope.ServiceProvider);

        try
        {
            // Await the handler, to keep the container scope alive
            await handler(payload, ct);
        }
        catch (OperationCanceledException e) when (e.CancellationToken == ct)
        {
            throw;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "{Queue}: unhandled exception while processing a message", _name);
        }
    }
}
