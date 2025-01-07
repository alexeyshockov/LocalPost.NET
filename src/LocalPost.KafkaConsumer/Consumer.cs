using Confluent.Kafka;
using LocalPost.DependencyInjection;
using LocalPost.Flow;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace LocalPost.KafkaConsumer;

internal sealed class Consumer(string name, ILogger logger,
    ClientFactory clientFactory, Handler<Event<ConsumeContext<byte[]>>> handler)
    : IHostedService, IHealthAwareService, IDisposable
{
    private Clients _clients = new([]);

    private CancellationTokenSource? _execTokenSource;
    private Task? _exec;
    private Exception? _execException;
    private string? _execExceptionDescription;

    private CancellationToken _completionToken = CancellationToken.None;

    private HealthCheckResult Ready => (_execTokenSource, _execution: _exec, _execException) switch
    {
        (null, _, _) => HealthCheckResult.Unhealthy("Not started"),
        (_, { IsCompleted: true }, _) => HealthCheckResult.Unhealthy("Stopped"),
        (not null, null, _) => HealthCheckResult.Degraded("Starting"),
        (not null, not null, null) => HealthCheckResult.Healthy("Running"),
        (_, _, not null) => HealthCheckResult.Unhealthy(_execExceptionDescription, _execException),
    };

    public IHealthCheck ReadinessCheck => HealthChecks.From(() => Ready);

    private async Task RunConsumerAsync(Client client, CancellationToken execToken)
    {
        // (Optionally) wait for app start

        try
        {
            while (!execToken.IsCancellationRequested)
            {
                var result = client.Consume(execToken);
                await handler(new ConsumeContext<byte[]>(client, result, result.Message.Value), CancellationToken.None)
                    .ConfigureAwait(false);
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

        logger.LogInformation("Starting Kafka consumer...");
        var clients = _clients = await clientFactory.Create(ct).ConfigureAwait(false);
        logger.LogInformation("Kafka consumer started");

        logger.LogDebug("Invoking the event handler...");
        await handler(Event<ConsumeContext<byte[]>>.Begin, ct).ConfigureAwait(false);
        logger.LogDebug("Event handler started");

        _exec = ObserveExecution();
        return;

        async Task ObserveExecution()
        {
            try
            {
                var executions = clients.Select(client =>
                    Task.Run(() => RunConsumerAsync(client, execTokenSource.Token), ct)
                ).ToArray();
                await (executions.Length == 1 ? executions[0] : Task.WhenAll(executions)).ConfigureAwait(false);

                // TODO Pass the exception (if any) to the handler
                await handler(Event<ConsumeContext<byte[]>>.End, _completionToken).ConfigureAwait(false);
            }
            finally
            {
                // Can happen before the service shutdown, in case of an error
                await _clients.Close(_completionToken).ConfigureAwait(false);
                logger.LogInformation("Kafka consumer stopped");
            }
        }
    }

    // await _execTokenSource.CancelAsync(); // .NET 8+
    private void CancelExecution() => _execTokenSource?.Cancel();

    public async Task StopAsync(CancellationToken forceShutdownToken)
    {
        if (_execTokenSource is null)
            throw new InvalidOperationException("Service has not been started");

        logger.LogInformation("Shutting down Kafka consumer...");

        _completionToken = forceShutdownToken;
        CancelExecution();
        if (_exec is not null)
            await _exec.ConfigureAwait(false);
    }

    public void Dispose()
    {
        _execTokenSource?.Dispose();
        _exec?.Dispose();
    }
}
