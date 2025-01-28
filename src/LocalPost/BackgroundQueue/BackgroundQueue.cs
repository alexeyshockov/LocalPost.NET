using System.Threading.Channels;
using LocalPost.DependencyInjection;
using LocalPost.Flow;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace LocalPost.BackgroundQueue;

internal sealed class BackgroundQueue<T>(ILogger<BackgroundQueue<T>> logger, QueueOptions<T> settings,
    Channel<ConsumeContext<T>> channel, IHandlerManager<ConsumeContext<T>> hm)
    : IBackgroundQueue<T>, IHostedService, IHealthAwareService, IDisposable
{
    public IHealthCheck ReadinessCheck => HealthChecks.From(() => _runner switch
    {
        not null => _runner.Ready,
        _ => HealthCheckResult.Unhealthy("Background queue has not started yet"),
    });

    public ValueTask Enqueue(T payload, CancellationToken ct = default) => channel.Writer.WriteAsync(payload, ct);

    public ChannelWriter<ConsumeContext<T>> Writer => channel.Writer;

    private ChannelRunner<ConsumeContext<T>, ConsumeContext<T>>? _runner;

    private ChannelRunner<ConsumeContext<T>, ConsumeContext<T>> CreateRunner(Handler<ConsumeContext<T>> handler)
    {
        return new ChannelRunner<ConsumeContext<T>, ConsumeContext<T>>(channel, Consume, hm)
            { Consumers = settings.MaxConcurrency };

        async Task Consume(CancellationToken execToken)
        {
            await foreach (var message in channel.Reader.ReadAllAsync(execToken).ConfigureAwait(false))
                await handler(message, CancellationToken.None).ConfigureAwait(false);
        }
    }

    public async Task StartAsync(CancellationToken ct)
    {
        var handler = await hm.Start(ct).ConfigureAwait(false);
        _runner = CreateRunner(handler);
        await _runner.Start(ct).ConfigureAwait(false);
    }

    public async Task StopAsync(CancellationToken forceShutdownToken)
    {
        if (_runner is null)
            return;

        logger.LogInformation("Shutting down background queue");
        try
        {
            // Wait until all the producers are done
            await settings.CompletionTrigger(forceShutdownToken).ConfigureAwait(false);
        }
        finally
        {
            await _runner.Stop(null, forceShutdownToken).ConfigureAwait(false);
        }
    }

    public void Dispose()
    {
        _runner?.Dispose();
    }
}
