using LocalPost.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LocalPost.BackgroundQueue.DependencyInjection;

[PublicAPI]
public class BackgroundQueuesBuilder(IServiceCollection services)
{
    public OptionsBuilder<QueueOptions<BackgroundJob>> AddDefaultJobQueue() => AddJobQueue(
        HandlerStack.For<BackgroundJob>(async (job, ct) => await job(ct).ConfigureAwait(false))
            .Scoped()
            .UsePayload()
            .Trace()
            .LogExceptions()
    );

    // TODO Open later
    internal OptionsBuilder<QueueOptions<BackgroundJob>> AddJobQueue(HandlerFactory<ConsumeContext<BackgroundJob>> hf)
    {
        services.TryAddSingleton<BackgroundJobQueue>();
        services.TryAddSingletonAlias<IBackgroundJobQueue, BackgroundJobQueue>();

        return AddQueue(hf);
    }

    private OptionsBuilder<QueueOptions<T>> AddQueue<T>(HandlerFactory<ConsumeContext<T>> hf)
    {
        // TODO Check if a non-keyed service should be added

        return AddQueue(Options.DefaultName, hf);
    }

    private OptionsBuilder<QueueOptions<T>> AddQueue<T>(string name, HandlerFactory<ConsumeContext<T>> hf)
    {
        if (!services.TryAddSingletonAlias<IBackgroundQueue<T>, BackgroundQueue<T>>(name))
            throw new InvalidOperationException(
                $"{Reflection.FriendlyNameOf<IBackgroundQueue<T>>(name)}> is already registered.");

        services.TryAddKeyedSingleton(name, (provider, _) => new BackgroundQueue<T>(
            provider.GetLoggerFor<BackgroundQueue<T>>(),
            provider.GetOptions<QueueOptions<T>>(name),
            hf(provider)
        ));
        services.AddHostedService(provider => provider.GetRequiredKeyedService<BackgroundQueue<T>>(name));

        return QueueFor<T>(name);
    }

    public OptionsBuilder<QueueOptions<T>> QueueFor<T>() =>
        services.AddOptions<QueueOptions<T>>();

    public OptionsBuilder<QueueOptions<T>> QueueFor<T>(string name) =>
        services.AddOptions<QueueOptions<T>>(name);
}
