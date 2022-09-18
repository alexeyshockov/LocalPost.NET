using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LocalPost.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBackgroundJobQueue(this IServiceCollection services,
        ushort maxConcurrency = ushort.MaxValue)
    {
        services.TryAddSingleton<BackgroundJobQueue>();
        services.TryAddSingleton<IBackgroundJobQueue>(provider => provider.GetRequiredService<BackgroundJobQueue>());

        return services.AddCustomBackgroundQueue<Job, BackgroundJobQueue>(_ => (job, ct) => job(ct), maxConcurrency);
    }

    public static IServiceCollection AddBackgroundQueue<T>(this IServiceCollection services,
        ushort maxConcurrency = ushort.MaxValue) => services.AddBackgroundQueue<T, IMessageHandler<T>>(maxConcurrency);

    // THandler has to be registered by the user
    public static IServiceCollection AddBackgroundQueue<T, THandler>(this IServiceCollection services,
        ushort maxConcurrency = ushort.MaxValue) where THandler : IMessageHandler<T> =>
        services.AddBackgroundQueue<T>(provider => provider.GetRequiredService<THandler>().Process, maxConcurrency);

    public static IServiceCollection AddBackgroundQueue<T>(this IServiceCollection services,
        Func<IServiceProvider, MessageHandler<T>> consumerFactory, ushort maxConcurrency = ushort.MaxValue) =>
        services
            .AddSingleton<BackgroundQueue<T>>()
            .AddSingleton<IBackgroundQueue<T>>(provider => provider.GetRequiredService<BackgroundQueue<T>>())
            .AddCustomBackgroundQueue<T, BackgroundQueue<T>>(consumerFactory, maxConcurrency);

    // THandler has to be registered by the user
    public static IServiceCollection AddCustomBackgroundQueue<T, TReader, THandler>(this IServiceCollection services,
        ushort maxConcurrency = ushort.MaxValue)
        where TReader : IBackgroundQueueReader<T>
        where THandler : IMessageHandler<T> => services.AddCustomBackgroundQueue<T, TReader>(
        provider => provider.GetRequiredService<THandler>().Process,
        maxConcurrency);

    public static IServiceCollection AddCustomBackgroundQueue<T, TReader>(this IServiceCollection services,
        Func<IServiceProvider, MessageHandler<T>> consumerFactory, ushort maxConcurrency = ushort.MaxValue)
        where TReader : IBackgroundQueueReader<T>
    {
        // TODO Try...() version of this one, to be gentle with multiple registrations?..
        services.AddHostedService(provider => ActivatorUtilities.CreateInstance<BackgroundQueueConsumer<T>>(provider,
            provider.GetRequiredService<TReader>(), new BoundedExecutor(maxConcurrency), consumerFactory));

        // TODO Health check (use ChannelReader<T>.Count property...)

        return services;
    }
}
