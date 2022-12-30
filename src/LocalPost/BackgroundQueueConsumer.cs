using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LocalPost;

internal sealed class BackgroundQueueConsumer<T> : BackgroundService
{
    private readonly ILogger<BackgroundQueueConsumer<T>> _logger;
    private readonly string _name;
    private readonly IServiceScopeFactory _scopeFactory;

    private readonly IAsyncEnumerable<T> _reader;
    private readonly IExecutor _executor;
    private readonly Func<IServiceProvider, MessageHandler<T>> _handlerFactory;

    public BackgroundQueueConsumer(string name,
        ILogger<BackgroundQueueConsumer<T>> logger, IServiceScopeFactory scopeFactory,
        IExecutor executor, IAsyncEnumerable<T> reader, Func<IServiceProvider, MessageHandler<T>> handlerFactory)
    {
        _logger = logger;
        _name = name;
        _scopeFactory = scopeFactory;
        _reader = reader;
        _executor = executor;
        _handlerFactory = handlerFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting {Name} background queue...", _name);

        try
        {
            await foreach (var message in _reader.WithCancellation(stoppingToken))
                await _executor.StartAsync(() => Process(message, stoppingToken), stoppingToken);
        }
        catch (OperationCanceledException e) when (e.CancellationToken == stoppingToken)
        {
            // The rest of the queue will be processed in StopAsync() below
            _logger.LogInformation("Application exit has been requested, stopping {Name} background queue...", _name);
        }
        catch (ChannelClosedException e)
        {
            _logger.LogWarning(e, "{Name} queue has been closed, stop listening", _name);

            // The rest of the queue will be processed in StopAsync() below
        }
        catch (Exception e)
        {
            // Custom error handler?..
            _logger.LogCritical(e, "Unhandled exception, stop listening");
        }
    }

    public override async Task StopAsync(CancellationToken forceExitToken)
    {
        await base.StopAsync(forceExitToken);

        var enumerator = _reader.GetAsyncEnumerator(forceExitToken);
        var move = enumerator.MoveNextAsync();
        var completed = false;
        do
        {
            // Suck out all the _available_ messages
            while (move.IsCompleted)
            {
                completed = !await move;
                if (completed)
                    break;

                await _executor.StartAsync(() => Process(enumerator.Current, forceExitToken), forceExitToken);

                move = enumerator.MoveNextAsync();
            }

            if (_executor.IsEmpty)
                // It means that nothing has been started (no messages read), so we are finally done
                break;

            // Wait until all currently running tasks are finished
            await _executor.WaitAsync(forceExitToken);
        } while (!completed);
    }

    private async Task Process(T message, CancellationToken ct)
    {
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
            _logger.LogError(e, "{Name} queue: unhandled exception while processing a message", _name);
        }
    }
}
