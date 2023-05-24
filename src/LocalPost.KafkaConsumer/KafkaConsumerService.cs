using System.Collections.Immutable;
using Confluent.Kafka;
using LocalPost.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace LocalPost.KafkaConsumer;

internal sealed class KafkaConsumerService<TKey, TValue> : INamedService
{
    public static KafkaConsumerService<TKey, TValue> Create(IServiceProvider provider, string name,
        MiddlewareStack<ConsumeContext<TKey, TValue>> handlerStack,
        Action<ConsumerBuilder<TKey, TValue>> configureClient)
    {
        var options = provider.GetOptions<Options>(name);

        var clientBuilder = new ConsumerBuilder<TKey, TValue>(options.Kafka);
        configureClient(clientBuilder);

        var kafkaClient = clientBuilder.Build();
        var messageSource = ActivatorUtilities.CreateInstance<MessageSource<TKey, TValue>>(provider,
            options.TopicName, kafkaClient);
        var queueSupervisor = ActivatorUtilities.CreateInstance<BackgroundServiceSupervisor>(provider,
            name, messageSource);

        HandlerFactory<ConsumeContext<TKey, TValue>> handlerFactory = handlerStack.Resolve;
        Handler<ConsumeContext<TKey, TValue>> handler =
            ActivatorUtilities.CreateInstance<ScopedHandler<ConsumeContext<TKey, TValue>>>(provider,
                name, handlerFactory).InvokeAsync;

        var consumers = Enumerable.Range(1, options.MaxConcurrency)
            .Select(_ =>
            {
                var consumer = new BackgroundQueue<ConsumeContext<TKey, TValue>>.Consumer(messageSource, handler);
                var supervisor = ActivatorUtilities.CreateInstance<BackgroundServiceSupervisor>(provider,
                    name, consumer);

                return supervisor;
            }).ToImmutableList();

        return new KafkaConsumerService<TKey, TValue>(name, options, queueSupervisor, consumers);
    }

    public KafkaConsumerService(string name, Options options,
        IBackgroundServiceSupervisor reader,
        IEnumerable<IBackgroundServiceSupervisor> consumers)
    {
        Name = name;
        Options = options;

        _queueReadinessCheck = new IBackgroundServiceSupervisor.ReadinessCheck(reader);
        _queueLivenessCheck = new IBackgroundServiceSupervisor.LivenessCheck(reader);

        var consumerGroup= new IBackgroundServiceSupervisor.Combined(consumers);
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
        provider => provider.GetRequiredService<KafkaConsumerService<TKey, TValue>>(name)._queueReadinessCheck,
        failureStatus,
        tags);

    public static HealthCheckRegistration QueueLivenessCheck(string name, HealthStatus? failureStatus = default,
        IEnumerable<string>? tags = default) => new(name,
        provider => provider.GetRequiredService<KafkaConsumerService<TKey, TValue>>(name)._queueLivenessCheck,
        failureStatus,
        tags);

    public static HealthCheckRegistration ConsumerGroupReadinessCheck(string name,
        HealthStatus? failureStatus = default,
        IEnumerable<string>? tags = default) => new(name,
        provider => provider.GetRequiredService<KafkaConsumerService<TKey, TValue>>(name)._consumerGroupReadinessCheck,
        failureStatus,
        tags);

    public static HealthCheckRegistration ConsumerGroupLivenessCheck(string name, HealthStatus? failureStatus = default,
        IEnumerable<string>? tags = default) => new(name,
        provider => provider.GetRequiredService<KafkaConsumerService<TKey, TValue>>(name)._consumerGroupLivenessCheck,
        failureStatus,
        tags);
}
