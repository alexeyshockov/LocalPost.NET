using System.Threading.Channels;
using LocalPost.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace LocalPost.BackgroundQueue;

internal sealed class BackgroundQueue<T>(ILogger<BackgroundQueue<T>> logger, QueueOptions<T> settings,
    Handler<ConsumeContext<T>> handler) : IBackgroundQueue<T>, IHostedService, IHealthAwareService, IDisposable
{
    private sealed class ReadinessHealthCheck(BackgroundQueue<T> queue) : IHealthCheck
    {
        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default) =>
            Task.FromResult(queue.Ready);
    }

    private CancellationTokenSource? _execTokenSource;
    private Task? _execution;
    private Exception? _execException;
    private string? _execExceptionDescription;

    private HealthCheckResult Ready => (_execTokenSource, _execution, _execException) switch
    {
        (null, _, _) => HealthCheckResult.Unhealthy("Not started"),
        (_, { IsCompleted: true }, _) => HealthCheckResult.Unhealthy("Stopped"),
        (not null, null, _) => HealthCheckResult.Degraded("Starting"),
        (not null, not null, null) => HealthCheckResult.Healthy("Running"),
        (_, _, not null) => HealthCheckResult.Unhealthy(_execExceptionDescription, _execException),
    };

    public IHealthCheck ReadinessCheck => new ReadinessHealthCheck(this);

    private readonly Channel<ConsumeContext<T>> _queue = settings.BufferSize switch
    {
        null => Channel.CreateUnbounded<ConsumeContext<T>>(new UnboundedChannelOptions
        {
            SingleReader = settings.MaxConcurrency == 1,
            SingleWriter = settings.SingleProducer,
        }),
        _ => Channel.CreateBounded<ConsumeContext<T>>(new BoundedChannelOptions(settings.BufferSize.Value)
        {
            FullMode = settings.FullMode,
            SingleReader = settings.MaxConcurrency == 1,
            SingleWriter = settings.SingleProducer,
        })
    };

    public ValueTask Enqueue(T payload, CancellationToken ct = default) => _queue.Writer.WriteAsync(payload, ct);

    public ChannelWriter<ConsumeContext<T>> Writer => _queue.Writer;

    private async Task RunAsync(CancellationToken execToken)
    {
        // (Optionally) wait for app start

        try
        {
            await foreach (var message in _queue.Reader.ReadAllAsync(execToken))
                await handler(message, CancellationToken.None);
        }
        catch (OperationCanceledException e) when (e.CancellationToken == execToken)
        {
            // logger.LogInformation("Background queue consumer shutdown");
        }
        catch (Exception e)
        {
            logger.LogCritical(e, "Background queue message handler error");
            (_execException, _execExceptionDescription) = (e, "Message handler failed");
        }
        finally
        {
            CloseChannel(); // Stop other consumers too
        }
    }

    private void CloseChannel() => _queue.Writer.Complete();

    private void CancelExecution() => _execTokenSource?.Cancel();

    public async Task StartAsync(CancellationToken ct)
    {
        if (_execTokenSource is not null)
            throw new InvalidOperationException("Service is already started");

        var execTokenSource = _execTokenSource = new CancellationTokenSource();
        _execution = ObserveExecution();
        await Task.Yield();
        return;

        async Task ObserveExecution()
        {
            var execution = settings.MaxConcurrency switch
            {
                1 => RunAsync(execTokenSource.Token),
                _ => Task.WhenAll(Enumerable.Range(0, settings.MaxConcurrency).Select(_ => RunAsync(execTokenSource.Token)))
            };
            await execution.ConfigureAwait(false);
            // Can happen before the service shutdown, in case of an error
            logger.LogInformation("Background queue stopped");
        }
    }

    public async Task StopAsync(CancellationToken forceShutdownToken)
    {
        if (_execTokenSource is null)
            throw new InvalidOperationException("Service has not been started");

        logger.LogInformation("Shutting down background queue");
        try
        {
            await settings.CompletionTrigger(forceShutdownToken).ConfigureAwait(false);
        }
        finally
        {
            CloseChannel();
        }
        if (_execution is not null)
            await _execution.ConfigureAwait(false);
    }

    public void Dispose()
    {
        _execTokenSource?.Dispose();
    }
}
