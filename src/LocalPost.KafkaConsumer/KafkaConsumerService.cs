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
        var reader = new BackgroundServiceSupervisor(messageSource);

        HandlerFactory<ConsumeContext<TKey, TValue>> handlerFactory = handlerStack.Resolve;
        Handler<ConsumeContext<TKey, TValue>> handler =
            ActivatorUtilities.CreateInstance<ScopedHandler<ConsumeContext<TKey, TValue>>>(provider,
                name, handlerFactory).InvokeAsync;

        var consumer = new BackgroundQueue<ConsumeContext<TKey, TValue>>.Consumer(messageSource, handler);
        var consumerGroup = new ConsumerGroup(consumer.Run, options.MaxConcurrency);

        return new KafkaConsumerService<TKey, TValue>(name, reader, consumerGroup);
    }

    public KafkaConsumerService(string name, IBackgroundServiceSupervisor reader,
        IBackgroundServiceSupervisor consumerGroup)
    {
        Name = name;

        Reader = reader;
        _readerReadinessCheck = new IBackgroundServiceSupervisor.ReadinessCheck(reader);
        _readerLivenessCheck = new IBackgroundServiceSupervisor.LivenessCheck(reader);

        ConsumerGroup = consumerGroup;
        _consumerGroupReadinessCheck = new IBackgroundServiceSupervisor.ReadinessCheck(consumerGroup);
        _consumerGroupLivenessCheck = new IBackgroundServiceSupervisor.LivenessCheck(consumerGroup);
    }

    public string Name { get; }

    // Expose only the root supervisor to the host, to avoid deadlocks (.NET runtime handles background services
    // synchronously by default, so if consumers are stopped first, they will block the reader from completing the
    // channel).
//    public IHostedService Supervisor { get; }

    public IConcurrentHostedService Reader { get; }
    private readonly IHealthCheck _readerReadinessCheck;
    private readonly IHealthCheck _readerLivenessCheck;

    public IConcurrentHostedService ConsumerGroup { get; }
    private readonly IHealthCheck _consumerGroupReadinessCheck;
    private readonly IHealthCheck _consumerGroupLivenessCheck;

    public static HealthCheckRegistration QueueReadinessCheck(string name, HealthStatus? failureStatus = default,
        IEnumerable<string>? tags = default) => new(name,
        provider => provider.GetRequiredService<KafkaConsumerService<TKey, TValue>>(name)._readerReadinessCheck,
        failureStatus,
        tags);

    public static HealthCheckRegistration QueueLivenessCheck(string name, HealthStatus? failureStatus = default,
        IEnumerable<string>? tags = default) => new(name,
        provider => provider.GetRequiredService<KafkaConsumerService<TKey, TValue>>(name)._readerLivenessCheck,
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
