using JetBrains.Annotations;
using LocalPost.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace LocalPost.SqsConsumer.DependencyInjection;

[PublicAPI]
public static class ServiceCollectionEx
{
    public static IServiceCollection AddSqsConsumers(this IServiceCollection services, Action<SqsBuilder> configure)
    {
        configure(new SqsBuilder(services));

        return services;
    }

    internal static bool TryAddQueueClient(this IServiceCollection services, string name) =>
        services.TryAddNamedSingleton(name, provider =>
            ActivatorUtilities.CreateInstance<QueueClient>(provider, name));
}
