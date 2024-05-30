using JetBrains.Annotations;
using LocalPost.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LocalPost.KafkaConsumer.DependencyInjection;

[PublicAPI]
public static class ServiceCollectionEx
{
    public static IServiceCollection AddKafkaConsumers(this IServiceCollection services, Action<KafkaBuilder> configure)
    {
        configure(new KafkaBuilder(services));

        return services;
    }

    internal static bool TryAddKafkaClient<TOptions>(this IServiceCollection services, string name)
        where TOptions : Options =>
        services.TryAddNamedSingleton(name, provider =>
        {
            var options = provider.GetOptions<TOptions>(name);

            return new KafkaTopicClient(provider.GetRequiredService<ILogger<KafkaTopicClient>>(),
                options, options.Topic, name);
        });
}
