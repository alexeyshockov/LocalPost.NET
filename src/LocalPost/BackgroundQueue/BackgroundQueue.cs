using System.Threading.Channels;
using LocalPost.DependencyInjection;
using LocalPost.Flow;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace LocalPost.BackgroundQueue;

internal sealed class BackgroundQueue<T>(ILogger<BackgroundQueue<T>> logger, QueueOptions<T> settings,
    Channel<ConsumeContext<T>> queue, ChannelRunner<ConsumeContext<T>, ConsumeContext<T>> runner)
    : IBackgroundQueue<T>, IHostedService, IHealthAwareService, IDisposable
{
    public IHealthCheck ReadinessCheck => HealthChecks.From(() => runner.Ready);

    public ValueTask Enqueue(T payload, CancellationToken ct = default) => queue.Writer.WriteAsync(payload, ct);

    public ChannelWriter<ConsumeContext<T>> Writer => queue.Writer;

    public async Task StartAsync(CancellationToken ct)
    {
        await runner.Start(ct).ConfigureAwait(false);
    }

    public async Task StopAsync(CancellationToken forceShutdownToken)
    {
        logger.LogInformation("Shutting down background queue");
        try
        {
            // Wait until all the producers are done
            await settings.CompletionTrigger(forceShutdownToken).ConfigureAwait(false);
        }
        finally
        {
            await runner.Stop(null, forceShutdownToken).ConfigureAwait(false);
        }
    }

    public void Dispose()
    {
        runner.Dispose();
    }
}
