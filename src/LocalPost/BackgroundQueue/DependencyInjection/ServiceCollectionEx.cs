using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;

namespace LocalPost.BackgroundQueue.DependencyInjection;

[PublicAPI]
public static class ServiceCollectionEx
{
    public static IServiceCollection AddBackgroundQueues(this IServiceCollection services,
        Action<BackgroundQueuesBuilder> configure)
    {
        configure(new BackgroundQueuesBuilder(services));

        return services;
    }
}
