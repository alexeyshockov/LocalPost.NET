using Microsoft.Extensions.DependencyInjection;

namespace LocalPost.KafkaConsumer.DependencyInjection;

[PublicAPI]
public static class ServiceCollectionEx
{
    public static KafkaBuilder AddKafkaConsumers(this IServiceCollection services) => new(services);
}
