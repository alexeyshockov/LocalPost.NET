using Confluent.Kafka;
using LocalPost.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace LocalPost.KafkaConsumer;

internal sealed class Consumer(string name, ILogger<Consumer> logger,
    ClientFactory clientFactory, ConsumerOptions settings, Handler<ConsumeContext<byte[]>> handler)
    : IHostedService, IHealthAwareService, IDisposable
{
    private sealed class ReadinessHealthCheck(Consumer consumer) : IHealthCheck
    {
        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default) =>
            Task.FromResult(consumer.Ready);
    }

    private CancellationTokenSource? _execTokenSource;
    private Task? _execution;
    private Exception? _execException;
    private string? _execExceptionDescription;

    public string Name { get; } = name;

    private HealthCheckResult Ready => (_execTokenSource, _execution, _execException) switch
    {
        (null, _, _) => HealthCheckResult.Unhealthy("Not started"),
        (_, { IsCompleted: true }, _) => HealthCheckResult.Unhealthy("Stopped"),
        (not null, null, _) => HealthCheckResult.Degraded("Starting"),
        (not null, not null, null) => HealthCheckResult.Healthy("Running"),
        (_, _, not null) => HealthCheckResult.Unhealthy(_execExceptionDescription, _execException),
    };

    public IHealthCheck ReadinessCheck => new ReadinessHealthCheck(this);

    private async Task RunConsumerAsync(Client client, CancellationToken execToken)
    {
        // (Optionally) wait for app start

        try
        {
            while (!execToken.IsCancellationRequested)
            {
                var result = client.Consume(execToken);
                await handler(new ConsumeContext<byte[]>(client, result, result.Message.Value), CancellationToken.None);
            }
        }
        catch (OperationCanceledException e) when (e.CancellationToken == execToken)
        {
            // logger.LogInformation("Kafka consumer shutdown");
        }
        catch (KafkaException e)
        {
            logger.LogCritical(e, "Kafka consumer error: {Reason} (see {HelpLink})", e.Error.Reason, e.HelpLink);
            (_execException, _execExceptionDescription) = (e, "Kafka consumer failed");
        }
        catch (Exception e)
        {
            logger.LogCritical(e, "Kafka message handler error");
            // TODO Include headers or the partition key in check result's data
            (_execException, _execExceptionDescription) = (e, "Message handler failed");
        }
        finally
        {
            CancelExecution(); // Stop other consumers too
        }
    }

    public async Task StartAsync(CancellationToken ct)
    {
        if (_execTokenSource is not null)
            throw new InvalidOperationException("Service is already started");

        var execTokenSource = _execTokenSource = new CancellationTokenSource();
        var execution = settings.Consumers switch
        {
            1 => await StartConsumerAsync(),
            _ => Task.WhenAll(
                await Task.WhenAll(Enumerable.Range(0, settings.Consumers).Select(_ => StartConsumerAsync())))
        };
        _execution = ObserveExecution();
        return;

        async Task<Task> StartConsumerAsync()
        {
            var kafkaClient = await Task.Run(() => clientFactory.Create(settings.ClientConfig, settings.Topics), ct);

            return Task.Run(() => RunConsumerAsync(kafkaClient, execTokenSource.Token), ct);
        }

        async Task ObserveExecution()
        {
            await execution;
            // Can happen before the service shutdown, in case of an error
            logger.LogInformation("Kafka consumer stopped");
        }
    }

    // await _execTokenSource.CancelAsync(); // .NET 8+
    private void CancelExecution() => _execTokenSource?.Cancel();

    public async Task StopAsync(CancellationToken forceShutdownToken)
    {
        if (_execTokenSource is null)
            throw new InvalidOperationException("Service has not been started");

        logger.LogInformation("Shutting down Kafka consumer");
        CancelExecution();
        if (_execution is not null)
            await _execution.ConfigureAwait(false);
    }

    public void Dispose()
    {
        _execTokenSource?.Dispose();
    }
}
