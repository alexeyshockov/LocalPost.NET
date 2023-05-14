using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace LocalPost.DependencyInjection;

public static class QueueRegistration
{
    // THandler has to be registered by the user
    public static OptionsBuilder<BackgroundQueueOptions<T>> AddBackgroundQueue<T, THandler>(this IServiceCollection services,
        Action<BackgroundQueue<T>.ConsumerBuilder>? configure = null) where THandler : IHandler<T> =>
        services.AddBackgroundQueue<T>(builder =>
        {
            builder.MiddlewareStackBuilder.SetHandler<THandler>();
            configure?.Invoke(builder);
        });

    public static OptionsBuilder<BackgroundQueueOptions<T>> AddBackgroundQueue<T>(this IServiceCollection services,
        Handler<T> handler, Action<BackgroundQueue<T>.ConsumerBuilder>? configure = null) =>
        services.AddBackgroundQueue<T>(builder =>
        {
            builder.MiddlewareStackBuilder.SetHandler(handler);
            configure?.Invoke(builder);
        });

    public static OptionsBuilder<BackgroundQueueOptions<T>> AddBackgroundQueue<T>(this IServiceCollection services,
        Action<BackgroundQueue<T>.ConsumerBuilder> configure)
    {
        services.TryAddSingleton<BackgroundQueue<T>>();
        services.TryAddSingleton<IBackgroundQueue<T>>(provider => provider.GetRequiredService<BackgroundQueue<T>>());

        services
            .AddBackgroundQueueConsumer<T, BackgroundQueue<T>>(configure)
            .Configure<IOptions<BackgroundQueueOptions<T>>>(
                (options, consumerOptions) => { options.MaxConcurrency = consumerOptions.Value.Consumer.MaxConcurrency; });

        return services.AddOptions<BackgroundQueueOptions<T>>();
    }

    // TReader has to be registered by the user
    public static OptionsBuilder<ConsumerOptions> AddBackgroundQueueConsumer<T, TReader>(this IServiceCollection services,
        Action<BackgroundQueue<T>.ConsumerBuilder> configure) where TReader : class, IAsyncEnumerable<T> =>
        services.AddBackgroundQueueConsumer<T>(Reflection.FriendlyNameOf<T>(), builder => configure(
            builder.SetReaderFactory(provider => provider.GetRequiredService<TReader>())));

    public static OptionsBuilder<ConsumerOptions> AddBackgroundQueueConsumer<T>(this IServiceCollection services,
        Action<BackgroundQueue<T>.ConsumerBuilder> configure) =>
        services.AddBackgroundQueueConsumer(Reflection.FriendlyNameOf<T>(), configure);

    public static OptionsBuilder<ConsumerOptions> AddBackgroundQueueConsumer<T>(this IServiceCollection services,
        string name, Action<BackgroundQueue<T>.ConsumerBuilder> configure)
    {
        var builder = new BackgroundQueue<T>.ConsumerBuilder(name);
        configure(builder);

        // TODO Try...() version of this one, to be gentle with multiple registrations of the same queue
        //      (extend ServiceDescriptor, add name to it and search using it)
        services.AddHostedService(builder.Build);

        return services.AddOptions<ConsumerOptions>(name);
    }
}
