using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace LocalPost.DependencyInjection;

public static class JobQueueRegistration
{
    public static OptionsBuilder<BackgroundQueueOptions<Job>> AddBackgroundJobQueue(this IServiceCollection services)
    {
        services.TryAddSingleton<BackgroundJobQueue>();
        services.TryAddSingleton<IBackgroundJobQueue>(provider => provider.GetRequiredService<BackgroundJobQueue>());

        return services.AddBackgroundQueue<Job>(builder =>
            builder.MiddlewareStackBuilder.SetHandler((job, ct) => job(ct)));
    }
}
