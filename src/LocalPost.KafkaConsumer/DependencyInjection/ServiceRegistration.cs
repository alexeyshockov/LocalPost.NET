using Confluent.Kafka;
using LocalPost.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace LocalPost.KafkaConsumer.DependencyInjection;

public static class ServiceRegistration
{
    public static OptionsBuilder<Options> AddKafkaConsumer<TValue>(this IServiceCollection services, string name,
        Action<MiddlewareStackBuilder<ConsumeContext<Ignore, TValue>>> configure,
        Action<ConsumerBuilder<Ignore, TValue>> configureClient) =>
        services.AddKafkaConsumer<Ignore, TValue>(name, configure, configureClient);

    public static OptionsBuilder<Options> AddKafkaConsumer<TKey, TValue>(this IServiceCollection services, string name,
        Action<MiddlewareStackBuilder<ConsumeContext<TKey, TValue>>> configure,
        Action<ConsumerBuilder<TKey, TValue>> configureClient)
    {
        var handleStackBuilder = new MiddlewareStackBuilder<ConsumeContext<TKey, TValue>>();
        configure(handleStackBuilder);
        var handlerStack = handleStackBuilder.Build();

        services.TryAddSingleton(provider => KafkaConsumerService<TKey, TValue>.Create(provider,
            name, handlerStack, configureClient));

        services.AddSingleton<IHostedService>(provider =>
            provider.GetRequiredService<KafkaConsumerService<TKey, TValue>>(name).Supervisor);

        // Extend ServiceDescriptor for better comparison and implement custom TryAddSingleton later...

        return services.AddOptions<Options>(name).Configure(options => options.TopicName = name);
    }
}
