using JetBrains.Annotations;
using LocalPost.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LocalPost.BackgroundQueue.DependencyInjection;

[PublicAPI]
public class BackgroundQueuesBuilder(IServiceCollection services)
{
    public OptionsBuilder<DefaultPipelineOptions<BackgroundJob>> AddJobQueue()
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

    public OptionsBuilder<DefaultBatchPipelineOptions<T>> AddBatchedQueue<T>(
        HandlerFactory<IEnumerable<ConsumeContext<T>>> hf)
    {
        var defaultPipeline = Pipeline
            .Create(hf, provider => provider.GetOptions<DefaultBatchPipelineOptions<T>>())
            .Buffer(1) // To avoid buffering for each concurrent IAsyncEnumerable consumer
            .Batch(provider => provider.GetOptions<DefaultBatchPipelineOptions<T>>());

        Add(defaultPipeline)
            .Configure<DefaultBatchPipelineOptions<T>>((options, pipelineOptions) =>
                options.UpdateFrom(pipelineOptions.Queue));

        return BatchedQueueFor<T>();
    }

    // // THandler has to be registered by the user
    // public OptionsBuilder<DefaultPipelineOptions<T>> AddQueue<T, THandler>() where THandler : IHandler<T> => AddQueue(
    //     // A way to configure the handler?..
    //     HandlerStack.From<THandler, T>()
    //         .Scoped()
    //         .UsePayload()
    //         .Trace()
    // );

    public OptionsBuilder<DefaultPipelineOptions<T>> AddQueue<T>(HandlerFactory<ConsumeContext<T>> hf)
    {
        var defaultPipeline = Pipeline
            .Create(hf, provider => provider.GetOptions<DefaultPipelineOptions<T>>());

        Add(defaultPipeline)
            .Configure<DefaultPipelineOptions<T>>((options, pipelineOptions) =>
                options.UpdateFrom(pipelineOptions.Queue));

        return QueueFor<T>();
    }

    internal OptionsBuilder<QueueOptions<T>> Add<T>(PipelineRegistration<ConsumeContext<T>> pr)
    {
        if (!services.TryAddSingletonAlias<IBackgroundQueue<T>, BackgroundQueue<T>>())
            // return ob; // Already added, don't register twice
            throw new InvalidOperationException(
                $"{Reflection.FriendlyNameOf<IBackgroundQueue<T>>()}> is already registered.");

        services.TryAddSingleton(BackgroundQueue.Create<T>);
        services.AddBackgroundService<BackgroundQueue<T>>();

        var context = services.RegistrationContextFor<IBackgroundQueue<T>>();
        pr(context, provider => provider.GetRequiredService<BackgroundQueue<T>>());
        // services.TryAddBackgroundConsumer<ConsumeContext<T>, BackgroundQueue<T, ConsumeContext<T>>>(hf, provider =>
        // {
        //     var options = provider.GetOptions<Options<T>>();
        //     return new ConsumerOptions(options.MaxConcurrency, false);
        // });

        return PipelineFor<T>();
    }

    public OptionsBuilder<QueueOptions<T>> PipelineFor<T>() => services.AddOptions<QueueOptions<T>>();

    public OptionsBuilder<DefaultPipelineOptions<T>> QueueFor<T>() =>
        services.AddOptions<DefaultPipelineOptions<T>>();

    public OptionsBuilder<DefaultBatchPipelineOptions<T>> BatchedQueueFor<T>() =>
        services.AddOptions<DefaultBatchPipelineOptions<T>>();



    // public OptionsBuilder<Options<T>> AddQueue<T>(HandlerFactory<ConsumeContext<T>> hf)
    // {
    //     if (!services.TryAddSingletonAlias<IBackgroundQueue<T>, BackgroundQueue<T, ConsumeContext<T>>>())
    //         // return ob; // Already added, don't register twice
    //         throw new InvalidOperationException($"BackgroundQueue<{Reflection.FriendlyNameOf<T>()}> is already registered.");
    //
    //     services.TryAddSingleton(provider => BackgroundQueue.Create<T>(provider.GetOptions<Options<T>>()));
    //     services.AddBackgroundService<BackgroundQueue<T, ConsumeContext<T>>>();
    //
    //     services.TryAddBackgroundConsumer<ConsumeContext<T>, BackgroundQueue<T, ConsumeContext<T>>>(hf, provider =>
    //     {
    //         var options = provider.GetOptions<Options<T>>();
    //         return new ConsumerOptions(options.MaxConcurrency, false);
    //     });
    //
    //     return services.AddOptions<Options<T>>();
    // }
}
