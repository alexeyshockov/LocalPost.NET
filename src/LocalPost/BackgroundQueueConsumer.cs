using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LocalPost;

internal sealed class BackgroundQueueConsumer<T> : BackgroundService
{
    private readonly ILogger<BackgroundQueueConsumer<T>> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    private readonly IBackgroundQueueReader<T> _queue;
    private readonly IExecutor _executor;
    private readonly Func<IServiceProvider, MessageHandler<T>> _consumerFactory;

    public BackgroundQueueConsumer(ILogger<BackgroundQueueConsumer<T>> logger, IServiceScopeFactory scopeFactory,
        IBackgroundQueueReader<T> queue, IExecutor executor, Func<IServiceProvider, MessageHandler<T>> consumerFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _queue = queue;
        _executor = executor;
        _consumerFactory = consumerFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var message in _queue.Reader.ReadAllAsync(stoppingToken))
                await _executor.Start(() => Process(message, stoppingToken));

            // Read the rest (all available currently) and process
            // TODO What is there are a lot of messages? Option to drop them and just wait for currently running ones...
            while (!_queue.Reader.Completion.IsCompleted && _queue.Reader.TryRead(out var message))
                await _executor.Start(() => Process(message, stoppingToken));
        }
        catch (ChannelClosedException)
        {
            _logger.LogWarning("Queue has been closed, stop listening");
        }
    }

    public override async Task StopAsync(CancellationToken forceExitToken)
    {
        await base.StopAsync(forceExitToken);

        // Wait until all currently running tasks are finished
        await _executor.WaitAsync(forceExitToken);
    }

    private async Task Process(T message, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();

        // Make it specific for this queue somehow?..
        var handler = _consumerFactory(scope.ServiceProvider);

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
            _logger.LogError(e, "Unhandled exception while processing a message");
        }
    }
}
