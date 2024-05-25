using Confluent.Kafka;
using JetBrains.Annotations;
using LocalPost.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LocalPost.KafkaConsumer.DependencyInjection;

[PublicAPI]
public sealed class KafkaBuilder(IServiceCollection services)
{
    // public IServiceCollection Services { get; }

    public OptionsBuilder<ClientConfig> Defaults { get; } = services.AddOptions<ClientConfig>();

//    public static OptionsBuilder<Options> AddKafkaConsumer<TValue>(this IServiceCollection Services, string name,
//        Action<HandlerStackBuilder<ConsumeContext<Ignore, TValue>>> configure,
//        Action<ConsumerBuilder<Ignore, TValue>> configureClient) =>
//        services.AddKafkaConsumer<Ignore, TValue>(name, configure, configureClient);

//    // JSON serializer is the default... But for Kafka it can be different?..
//    public static OptionsBuilder<Options> AddKafkaConsumer<THandler, T>(this IServiceCollection Services,
//        string name) where THandler : IHandler<ConsumeContext<T>> => services.AddKafkaConsumer(name, provider =>
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

        if (!services.TryAddKafkaClient<Options>(name))
            throw new ArgumentException("Kafka consumer is already registered", nameof(name));

        services.TryAddNamedSingleton(name, provider =>
            new MessageSource(provider.GetRequiredService<KafkaTopicClient>(name)));
        services.AddBackgroundServiceForNamed<MessageSource>(name);

        services.TryAddBackgroundConsumer<ConsumeContext<byte[]>, MessageSource>(name, configure, provider =>
        {
            var options = provider.GetOptions<Options>(name);
            return new ConsumerOptions(1, options.BreakOnException);
        });

        return services.AddOptions<Options>(name).Configure<IOptions<ClientConfig>>((options, commonConfig) =>
        {
            options.EnrichFrom(commonConfig.Value);
            options.Topic = name;
        });
    }

    public OptionsBuilder<BatchedOptions> AddBatchConsumer(string name,
        HandlerFactory<BatchConsumeContext<byte[]>> configure)
    {
        if (string.IsNullOrEmpty(name))
            throw new ArgumentException("A proper (non empty) name is required", nameof(name));

        if (!services.TryAddKafkaClient<BatchedOptions>(name))
            throw new InvalidOperationException("Kafka consumer is already registered");

        services.TryAddNamedSingleton(name, provider =>
        {
            var options = provider.GetOptions<BatchedOptions>(name);

            return new BatchMessageSource(provider.GetRequiredService<KafkaTopicClient>(name),
                ConsumeContext.BatchBuilder(
                    options.BatchMaxSize, TimeSpan.FromMilliseconds(options.BatchTimeWindowMilliseconds)));
        });
        services.AddBackgroundServiceForNamed<BatchMessageSource>(name);

        services.TryAddBackgroundConsumer<BatchConsumeContext<byte[]>, BatchMessageSource>(name, configure, provider =>
        {
            var options = provider.GetOptions<BatchedOptions>(name);
            return new ConsumerOptions(1, options.BreakOnException);
        });

        return services.AddOptions<BatchedOptions>(name).Configure<IOptions<ClientConfig>>((options, commonConfig) =>
        {
            options.EnrichFrom(commonConfig.Value);
            options.Topic = name;
        });
    }
}
