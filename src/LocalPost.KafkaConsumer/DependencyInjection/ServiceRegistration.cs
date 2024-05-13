using JetBrains.Annotations;
using LocalPost.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LocalPost.KafkaConsumer.DependencyInjection;

[PublicAPI]
public static class ServiceRegistration
{
    public static KafkaBuilder AddKafka(this IServiceCollection services, string? name = null) =>
        new(services, name);

    internal static bool TryAddKafkaClient<TOptions>(this IServiceCollection services, string name)
        where TOptions : Options =>
        services.TryAddNamedSingleton(name, provider =>
        {
            var options = provider.GetOptions<TOptions>(name);

            return new KafkaTopicClient(provider.GetRequiredService<ILogger<KafkaTopicClient>>(),
                options.Kafka, options.Topic, name);
        });

    // Wrap it into an internal class, to avoid collision with other libraries?..
//    public static OptionsBuilder<ConsumerConfig> ConfigureKafkaConsumerDefaults(this IServiceCollection Services,
//        Action<ConsumerConfig> configure) =>
//        // FIXME EnableAutoOffsetStore
//        Services.AddOptions<ConsumerConfig>().Configure(configure);
}
