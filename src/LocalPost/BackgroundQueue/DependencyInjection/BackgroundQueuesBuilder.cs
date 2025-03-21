using System.Threading.Channels;
using LocalPost.DependencyInjection;
using LocalPost.Flow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LocalPost.BackgroundQueue.DependencyInjection;

[PublicAPI]
public class BackgroundQueuesBuilder(IServiceCollection services)
{
    public OptionsBuilder<QueueOptions<BackgroundJob>> AddDefaultJobQueue() => AddJobQueue(
        HandlerStack.For<BackgroundJob>(async (job, ct) => await job(ct).ConfigureAwait(false))
            .Scoped()
            .UseMessagePayload()
            .Trace()
            .LogExceptions()
    );

    // TODO Open later
    internal OptionsBuilder<QueueOptions<BackgroundJob>> AddJobQueue(
        HandlerManagerFactory<ConsumeContext<BackgroundJob>> hmf)
    {
        services.TryAddSingleton<BackgroundJobQueue>();
        services.TryAddSingletonAlias<IBackgroundJobQueue, BackgroundJobQueue>();

        return AddQueue(hmf);
    }

    // public OptionsBuilder<QueueOptions<T>> AddQueue<T>(HandlerFactory<ConsumeContext<T>> hf) =>
    //     AddQueue(Options.DefaultName, hf);
    //
    // public OptionsBuilder<QueueOptions<T>> AddQueue<T>(string name, HandlerFactory<ConsumeContext<T>> hf) =>
    //     AddQueue<T>(name, provider => new HandlerManager<T>(hf(provider)));

    public OptionsBuilder<QueueOptions<T>> AddQueue<T>(HandlerManagerFactory<ConsumeContext<T>> hmf) =>
        AddQueue(Options.DefaultName, hmf);

    public OptionsBuilder<QueueOptions<T>> AddQueue<T>(string name, HandlerManagerFactory<ConsumeContext<T>> hmf)
    {
        if (!services.TryAddSingletonAlias<IBackgroundQueue<T>, BackgroundQueue<T>>(name))
            // throw new InvalidOperationException(
            //     $"{Reflection.FriendlyNameOf<IBackgroundQueue<T>>(name)}> is already registered.");
            throw new ArgumentException("Queue is already registered", nameof(name));

        services.TryAddKeyedSingleton(name, CreateQueue);
        services.AddHostedService(provider => provider.GetRequiredKeyedService<BackgroundQueue<T>>(name));

        return QueueFor<T>(name);

        BackgroundQueue<T> CreateQueue(IServiceProvider provider, object? _)
        {
            var settings = provider.GetOptions<QueueOptions<T>>(name);
            var channel = settings.Capacity switch
            {
                null => Channel.CreateUnbounded<ConsumeContext<T>>(new UnboundedChannelOptions
                {
                    SingleReader = settings.MaxConcurrency == 1,
                    SingleWriter = settings.SingleProducer,
                }),
                _ => Channel.CreateBounded<ConsumeContext<T>>(new BoundedChannelOptions(settings.Capacity.Value)
                {
                    FullMode = settings.FullMode,
                    SingleReader = settings.MaxConcurrency == 1,
                    SingleWriter = settings.SingleProducer,
                })
            };
            var hm = hmf(provider);

            return new BackgroundQueue<T>(provider.GetLoggerFor<BackgroundQueue<T>>(), settings, channel, hm);
        }
    }

    public OptionsBuilder<QueueOptions<T>> QueueFor<T>() =>
        services.AddOptions<QueueOptions<T>>();

    public OptionsBuilder<QueueOptions<T>> QueueFor<T>(string name) =>
        services.AddOptions<QueueOptions<T>>(name);
}
