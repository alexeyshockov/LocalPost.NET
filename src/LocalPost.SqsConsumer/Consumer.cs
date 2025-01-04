using Amazon.Runtime;
using Amazon.SQS;
using LocalPost.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace LocalPost.SqsConsumer;

internal sealed class Consumer(string name, ILogger<Consumer> logger, IAmazonSQS sqs,
    ConsumerOptions settings, Handler<ConsumeContext<string>> handler)
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

    private async Task RunConsumerAsync(QueueClient client, CancellationToken execToken)
    {
        // (Optionally) wait for app start

        try
        {
            while (!execToken.IsCancellationRequested)
            {
                var messages = await client.PullMessages(execToken).ConfigureAwait(false);
                await Task.WhenAll(messages
                    .Select(message => new ConsumeContext<string>(client, message, message.Body))
                    .Select(context => handler(context, CancellationToken.None).AsTask()))
                    .ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException e) when (e.CancellationToken == execToken)
        {
            // logger.LogInformation("SQS consumer shutdown");
        }
        catch (AmazonServiceException e)
        {
            logger.LogCritical(e, "SQS consumer error: {ErrorCode} (see {HelpLink})", e.ErrorCode, e.HelpLink);
            (_execException, _execExceptionDescription) = (e, "SQS consumer failed");
        }
        catch (Exception e)
        {
            logger.LogCritical(e, "SQS message handler error");
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

        var client = new QueueClient(logger, sqs, settings);
        await client.Connect(ct).ConfigureAwait(false);

        _execution = ObserveExecution();
        return;

        async Task ObserveExecution()
        {
            var execution = settings.Consumers switch
            {
                1 => RunConsumerAsync(client, execTokenSource.Token),
                _ => Task.WhenAll(Enumerable
                    .Range(0, settings.Consumers)
                    .Select(_ => RunConsumerAsync(client, execTokenSource.Token)))
            };
            await execution.ConfigureAwait(false);
            // Can happen before the service shutdown, in case of an error
            logger.LogInformation("SQS consumer stopped");
        }
    }

    // await _execTokenSource.CancelAsync(); // .NET 8+
    private void CancelExecution() => _execTokenSource?.Cancel();

    public async Task StopAsync(CancellationToken forceShutdownToken)
    {
        if (_execTokenSource is null)
            throw new InvalidOperationException("Service has not been started");

        logger.LogInformation("Shutting down SQS consumer");
        CancelExecution();
        if (_execution is not null)
            await _execution.ConfigureAwait(false);
    }

    public void Dispose()
    {
        _execTokenSource?.Dispose();
    }
}
