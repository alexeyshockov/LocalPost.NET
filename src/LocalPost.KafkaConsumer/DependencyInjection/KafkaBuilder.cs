using Confluent.Kafka;
using JetBrains.Annotations;
using LocalPost.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LocalPost.KafkaConsumer.DependencyInjection;

[PublicAPI]
public sealed class KafkaBuilder : OptionsBuilder<ClientConfig>
{
    public KafkaBuilder(IServiceCollection services, string? name) : base(services, name)
    {
    }

//    public static OptionsBuilder<Options> AddKafkaConsumer<TValue>(this IServiceCollection Services, string name,
//        Action<HandlerStackBuilder<ConsumeContext<Ignore, TValue>>> configure,
//        Action<ConsumerBuilder<Ignore, TValue>> configureClient) =>
//        Services.AddKafkaConsumer<Ignore, TValue>(name, configure, configureClient);

//    // JSON serializer is the default... But for Kafka it can be different?..
//    public static OptionsBuilder<Options> AddKafkaConsumer<THandler, T>(this IServiceCollection Services,
//        string name) where THandler : IHandler<ConsumeContext<T>> => Services.AddKafkaConsumer(name, provider =>
//    {
//        // Keep .Scoped() as far as possible, as from that point all the middlewares will be resolved per request, not
//        // just once
//        var handlerFactory = HandlerStack2.From<THandler, ConsumeContext<T>>().Scoped()
//            .Deserialize(null)
//            // TODO Error handler, just log all the errors and proceed (to not break the loop)
//            .Acknowledge();
//
//        return handlerFactory(provider);
//    });

    public OptionsBuilder<Options> AddConsumer(string name, HandlerFactory<ConsumeContext<byte[]>> configure)
    {
        if (string.IsNullOrEmpty(name)) // TODO Just default (empty?) name...
            throw new ArgumentException("A proper (non empty) name is required", nameof(name));

        // Services.AddSingleton<AcknowledgeMiddleware>();

        if (!Services.TryAddKafkaClient<Options>(name))
            throw new ArgumentException("Kafka consumer is already registered", nameof(name));

        Services.TryAddNamedSingleton(name, provider =>
            new MessageSource(provider.GetRequiredService<KafkaTopicClient>(name)));
        Services.AddBackgroundServiceForNamed<MessageSource>(name);

        Services.TryAddConsumerGroup<ConsumeContext<byte[]>, MessageSource>(name, configure);

        return Services.AddOptions<Options>(name).Configure<IOptionsMonitor<ClientConfig>>((options, commonConfigs) =>
        {
            var commonConfig = commonConfigs.Get(Name);

            options.Topic = name;
            options.Kafka = new ConsumerConfig(commonConfig)
            {
                EnableAutoOffsetStore = false, // We will store offsets manually, see ConsumeContext class
            };
        });
    }

    public OptionsBuilder<BatchedOptions> AddBatchConsumer(string name,
        HandlerFactory<BatchConsumeContext<byte[]>> configure)
    {
        if (string.IsNullOrEmpty(name)) // TODO Just default (empty?) name...
            throw new ArgumentException("A proper (non empty) name is required", nameof(name));

        // Services.AddSingleton<AcknowledgeMiddleware>();

        if (!Services.TryAddKafkaClient<BatchedOptions>(name))
            throw new ArgumentException("Kafka consumer is already registered", nameof(name));

        Services.TryAddNamedSingleton(name, provider =>
        {
            var options = provider.GetOptions<BatchedOptions>(name);

            return new BatchMessageSource(provider.GetRequiredService<KafkaTopicClient>(name),
                ConsumeContext.BatchBuilder(
                    options.BatchMaxSize, TimeSpan.FromMilliseconds(options.BatchTimeWindowMilliseconds)));
        });
        Services.AddBackgroundServiceForNamed<BatchMessageSource>(name);

        Services.TryAddConsumerGroup<BatchConsumeContext<byte[]>, BatchMessageSource>(name, configure);
//        Services.TryAddConsumerGroup(name, provider => BackgroundQueue.ConsumerGroupFor(
//            provider.GetRequiredService<BatchMessageSource>(name), configure(provider), 1));

        return Services.AddOptions<BatchedOptions>(name).Configure<IOptionsMonitor<ClientConfig>>((options, commonConfigs) =>
        {
            var commonConfig = commonConfigs.Get(Name);

            options.Topic = name;
            options.Kafka = new ConsumerConfig(commonConfig)
            {
                EnableAutoOffsetStore = false, // We will store offsets manually, see ConsumeContext class
            };
        });
    }
}
