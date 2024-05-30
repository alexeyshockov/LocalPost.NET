using JetBrains.Annotations;
using LocalPost.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LocalPost.BackgroundQueue.DependencyInjection;

[PublicAPI]
public class BackgroundQueuesBuilder(IServiceCollection services)
{
    public OptionsBuilder<Options<BackgroundJob>> AddJobQueue()
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
    public OptionsBuilder<Options<T>> AddQueue<T, THandler>() where THandler : IHandler<T> => AddQueue(
        // A way to configure the pipeline?..
        HandlerStack.From<THandler, T>()
            .Scoped()
            .UsePayload()
            .Trace()
    );

    public OptionsBuilder<Options<T>> AddQueue<T>(HandlerFactory<ConsumeContext<T>> hf)
    {
        if (!services.TryAddSingletonAlias<IBackgroundQueue<T>, BackgroundQueue<T, ConsumeContext<T>>>())
            // return ob; // Already added, don't register twice
            throw new InvalidOperationException($"BackgroundQueue<{Reflection.FriendlyNameOf<T>()}> is already registered.");

        services.TryAddSingleton(provider =>
            BackgroundQueue.Create<T>(provider.GetOptions<Options<T>>()));
        services.AddBackgroundServiceFor<BackgroundQueue<T, ConsumeContext<T>>>();

        services.TryAddBackgroundConsumer<ConsumeContext<T>, BackgroundQueue<T, ConsumeContext<T>>>(hf, provider =>
        {
            var options = provider.GetOptions<Options<T>>();
            return new ConsumerOptions(options.MaxConcurrency, false);
        });

        return services.AddOptions<Options<T>>();
    }

    // TODO Batched
}
