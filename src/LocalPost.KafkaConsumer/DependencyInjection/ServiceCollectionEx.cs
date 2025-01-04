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
}
