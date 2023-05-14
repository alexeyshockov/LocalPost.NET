using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LocalPost;

internal sealed class BackgroundQueueConsumer<T> : IBackgroundService
{
    private readonly ILogger<BackgroundQueueConsumer<T>> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    private readonly IAsyncEnumerable<T> _reader;
    private readonly IExecutor _executor;
    private readonly HandlerFactory<T> _handlerFactory;

    public BackgroundQueueConsumer(ILogger<BackgroundQueueConsumer<T>> logger, string name,
        IServiceScopeFactory scopeFactory,
        IExecutor executor,
        IAsyncEnumerable<T> reader,
        HandlerFactory<T> handlerFactory)
    {
        Name = name;
        _logger = logger;
        _scopeFactory = scopeFactory;
        _reader = reader;
        _executor = executor;
        _handlerFactory = handlerFactory;
    }

    public string Name { get; }

    public Task StartAsync(CancellationToken ct) => Task.CompletedTask;

    public async Task ExecuteAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var message in _reader.WithCancellation(ct))
                await _executor.StartAsync(() => Process(message, ct), ct);
        }
        catch (ChannelClosedException e)
        {
            _logger.LogWarning(e, "{Name} queue has been closed, stop listening", Name);

            // All currently running tasks will be processed in StopAsync() below
        }
    }

    public async Task StopAsync(CancellationToken forceExitToken)
    {
        // Good to have later: an option to NOT process the rest of the messages
        try
        {
            // TODO An option to NOT process the rest of the messages...
            await foreach (var message in _reader.WithCancellation(forceExitToken))
                await _executor.StartAsync(() => Process(message, forceExitToken), forceExitToken);
        }
        catch (ChannelClosedException)
        {
            // OK, just wait for the rest of the tasks to finish
        }

        // Wait until all currently running tasks are finished
        await _executor.WaitAsync(forceExitToken);
    }

    private async Task Process(T message, CancellationToken ct)
    {
        // TODO Tracing...

        using var scope = _scopeFactory.CreateScope();

        // Make it specific for this queue somehow?..
        var handler = _handlerFactory(scope.ServiceProvider);

        try
        {
            // Await the handler, to keep the container scope alive
            await handler(message, ct);
        }
        catch (OperationCanceledException e) when (e.CancellationToken == ct)
        {
            throw;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "{Queue}: unhandled exception while processing a message", Name);
        }
    }
}
