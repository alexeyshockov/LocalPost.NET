using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace LocalPost.DependencyInjection;

public static class JobQueueRegistrationExtensions
{
    public static OptionsBuilder<QueueOptions> AddBackgroundJobQueue(this IServiceCollection services)
    {
        services.TryAddSingleton<BackgroundJobQueue>();
        services.TryAddSingleton<IBackgroundJobQueue>(provider => provider.GetRequiredService<BackgroundJobQueue>());

        return services.AddCustomBackgroundQueue<Job, BackgroundJobQueue>(_ => (job, ct) => job(ct));
    }
}
