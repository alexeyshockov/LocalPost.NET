using Microsoft.Extensions.DependencyInjection;

namespace LocalPost.BackgroundQueue.DependencyInjection;

[PublicAPI]
public static class ServiceCollectionEx
{
    public static BackgroundQueuesBuilder AddBackgroundQueues(this IServiceCollection services) => new(services);
}
