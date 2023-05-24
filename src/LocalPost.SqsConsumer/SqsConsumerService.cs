using System.Collections.Immutable;
using LocalPost.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace LocalPost.SqsConsumer;

internal sealed class SqsConsumerService : INamedService
{
    public static SqsConsumerService Create(IServiceProvider provider, string name,
        MiddlewareStack<ConsumeContext> handlerStack)
    {
        var options = provider.GetOptions<Options>(name);

        var client = ActivatorUtilities.CreateInstance<QueueClient>(provider, options);
        var messageSource = new MessageSource(client);
        var queueSupervisor = ActivatorUtilities.CreateInstance<BackgroundServiceSupervisor>(provider, name, messageSource);

        HandlerFactory<ConsumeContext> handlerFactory = handlerStack.Resolve;
        Handler<ConsumeContext> handler = ActivatorUtilities.CreateInstance<ScopedHandler<ConsumeContext>>(provider,
            name, handlerFactory).InvokeAsync;

        var consumers = Enumerable.Range(1, options.MaxConcurrency)
            .Select(_ =>
            {
                var consumer = new BackgroundQueue<ConsumeContext>.Consumer(messageSource, handler);
                var supervisor = ActivatorUtilities.CreateInstance<BackgroundServiceSupervisor>(provider,
                    name, consumer);

                return supervisor;
            }).ToImmutableList();

        return new SqsConsumerService(name, options, queueSupervisor, consumers);
    }

    public SqsConsumerService(string name, Options options,
        IBackgroundServiceSupervisor reader,
        IEnumerable<IBackgroundServiceSupervisor> consumers)
    {
        Name = name;
        Options = options;

        _queueReadinessCheck = new IBackgroundServiceSupervisor.ReadinessCheck(reader);
        _queueLivenessCheck = new IBackgroundServiceSupervisor.LivenessCheck(reader);

        var consumerGroup = new IBackgroundServiceSupervisor.Combined(consumers);
        _consumerGroupReadinessCheck = new IBackgroundServiceSupervisor.ReadinessCheck(consumerGroup);
        _consumerGroupLivenessCheck = new IBackgroundServiceSupervisor.LivenessCheck(consumerGroup);

        Supervisor = new CombinedHostedService(reader, consumerGroup);
    }

    public string Name { get; }

    public Options Options { get; }

    // Expose only the root supervisor to the host, to avoid deadlocks (.NET runtime handles background services
    // synchronously by default, so if consumers are stopped first, they will block the reader from completing the
    // channel).
    public IHostedService Supervisor { get; }

    private readonly IHealthCheck _queueReadinessCheck;
    private readonly IHealthCheck _queueLivenessCheck;

    private readonly IHealthCheck _consumerGroupReadinessCheck;
    private readonly IHealthCheck _consumerGroupLivenessCheck;

    public static HealthCheckRegistration QueueReadinessCheck(string name, HealthStatus? failureStatus = default,
        IEnumerable<string>? tags = default) => new(name,
        provider => provider.GetRequiredService<SqsConsumerService>(name)._queueReadinessCheck,
        failureStatus,
        tags);

    public static HealthCheckRegistration QueueLivenessCheck(string name, HealthStatus? failureStatus = default,
        IEnumerable<string>? tags = default) => new(name,
        provider => provider.GetRequiredService<SqsConsumerService>(name)._queueLivenessCheck,
        failureStatus,
        tags);

    public static HealthCheckRegistration ConsumerGroupReadinessCheck(string name, HealthStatus? failureStatus = default,
        IEnumerable<string>? tags = default) => new(name,
        provider => provider.GetRequiredService<SqsConsumerService>(name)._consumerGroupReadinessCheck,
        failureStatus,
        tags);

    public static HealthCheckRegistration ConsumerGroupLivenessCheck(string name, HealthStatus? failureStatus = default,
        IEnumerable<string>? tags = default) => new(name,
        provider => provider.GetRequiredService<SqsConsumerService>(name)._consumerGroupLivenessCheck,
        failureStatus,
        tags);
}
