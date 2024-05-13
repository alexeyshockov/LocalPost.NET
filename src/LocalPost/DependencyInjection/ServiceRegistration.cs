using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LocalPost.DependencyInjection;

[PublicAPI]
public static class ServiceRegistration
{
    public static OptionsBuilder<BackgroundQueueOptions<Job>> AddBackgroundJobQueue(this IServiceCollection services)
    {
        services.TryAddSingleton<BackgroundJobQueue>();
        services.TryAddSingleton<IBackgroundJobQueue>(provider => provider.GetRequiredService<BackgroundJobQueue>());

        return services.AddBackgroundQueue<Job>(_ => async (job, ct) => await job(ct));
    }

    // THandler has to be registered by the user
    public static OptionsBuilder<BackgroundQueueOptions<T>> AddBackgroundQueue<T, THandler>(
        this IServiceCollection services,
        HandlerFactory<T>? configure = null) where THandler : IHandler<T> =>
        services.AddBackgroundQueue(HandlerStack.From<THandler, T>().Scoped());

    public static OptionsBuilder<BackgroundQueueOptions<T>> AddBackgroundQueue<T>(this IServiceCollection services,
        HandlerFactory<T> configure)
    {
        services.TryAddSingleton<IBackgroundQueue<T>>(provider => provider.GetRequiredService<BackgroundQueue<T, T>>());
        services.TryAddSingleton(provider =>
            BackgroundQueue.Create<T>(provider.GetOptions<BackgroundQueueOptions<T>>()));
        services.AddBackgroundServiceFor<BackgroundQueue<T, T>>();

        // FIXME Prevent adding two services with different handlers... Do not allow calling this method twice for the same queue?
        services.TryAddConsumerGroup<T, BackgroundQueue<T, T>>(configure);

        return services.AddOptions<BackgroundQueueOptions<T>>();
    }

    // TODO Batched
}
