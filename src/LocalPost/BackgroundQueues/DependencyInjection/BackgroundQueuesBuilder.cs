using JetBrains.Annotations;
using LocalPost.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LocalPost.BackgroundQueues.DependencyInjection;

[PublicAPI]
public class BackgroundQueuesBuilder(IServiceCollection services)
{
    public OptionsBuilder<BackgroundQueueOptions<BackgroundJob>> AddJobQueue()
    {
        services.TryAddSingleton<BackgroundJobQueue>();
        services.TryAddSingletonAlias<IBackgroundJobQueue, BackgroundJobQueue>();

        // TODO Allow to configure the handler somehow
        return AddQueue(
            HandlerStack.For<BackgroundJob>(async (job, ct) => await job(ct))
                .Scoped()
                .UsePayload()
                .Trace()
        );
    }

    // THandler has to be registered by the user
    public OptionsBuilder<BackgroundQueueOptions<T>> AddQueue<T, THandler>()
        where THandler : IHandler<T> =>
        AddQueue(
            HandlerStack.From<THandler, T>()
                .Scoped()
                .UsePayload()
                .Trace()
        );

    public OptionsBuilder<BackgroundQueueOptions<T>> AddQueue<T>(HandlerFactory<ConsumeContext<T>> hf)
    {
        if (!services.TryAddSingletonAlias<IBackgroundQueue<T>, BackgroundQueue<T, ConsumeContext<T>>>())
            // return ob; // Already added, don't register twice
            throw new InvalidOperationException($"BackgroundQueue<{Reflection.FriendlyNameOf<T>()}> is already registered.");

        services.TryAddSingleton(provider =>
            BackgroundQueue.Create<T>(provider.GetOptions<BackgroundQueueOptions<T>>()));
        services.AddBackgroundServiceFor<BackgroundQueue<T, ConsumeContext<T>>>();

        services.TryAddBackgroundConsumer<ConsumeContext<T>, BackgroundQueue<T, ConsumeContext<T>>>(hf, provider =>
        {
            var options = provider.GetOptions<BackgroundQueueOptions<T>>();
            return new ConsumerOptions(options.MaxConcurrency, false);
        });

        return services.AddOptions<BackgroundQueueOptions<T>>();
    }

    // TODO Batched
}
