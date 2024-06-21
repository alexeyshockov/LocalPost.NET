using JetBrains.Annotations;
using LocalPost.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace LocalPost.KafkaConsumer.DependencyInjection;

[PublicAPI]
public static class ServiceCollectionEx
{
    public static IServiceCollection AddKafkaConsumers(this IServiceCollection services, Action<KafkaBuilder> configure)
    {
        configure(new KafkaBuilder(services));

        return services;
    }

    internal static bool TryAddKafkaClient(this IServiceCollection services, string name) =>
        services.TryAddNamedSingleton(name, provider =>
        {
            var options = provider.GetOptions<ConsumerOptions>(name);

            return new KafkaTopicClient(provider.GetLoggerFor<KafkaTopicClient>(), options, options.Topic, name);
        });
}
