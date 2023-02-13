using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace LocalPost.DependencyInjection;

public static class QueueRegistration
{
    // THandler has to be registered by the user
    public static OptionsBuilder<QueueOptions> AddBackgroundQueue<T, THandler>(this IServiceCollection services,
        Action<BackgroundQueue<T>.Builder>? configure = null) where THandler : IHandler<T> =>
        services.AddBackgroundQueue<T>(builder => builder.MiddlewareStackBuilder.SetHandler<THandler>());

    public static OptionsBuilder<QueueOptions> AddBackgroundQueue<T>(this IServiceCollection services,
        Action<BackgroundQueue<T>.Builder> configure)
    {
        services.TryAddSingleton<BackgroundQueue<T>>();
        services.TryAddSingleton<IBackgroundQueue<T>>(provider => provider.GetRequiredService<BackgroundQueue<T>>());

        return services.AddBackgroundQueueConsumer<T, BackgroundQueue<T>>(configure);
    }



//    // TReader & THandler have to be registered by the user
//    public static OptionsBuilder<QueueOptions> AddBackgroundQueueConsumer<T, TReader, THandler>(
//        this IServiceCollection services) where TReader : IAsyncEnumerable<T> where THandler : IHandler<T> =>
//        services.AddCustomBackgroundQueue<T, TReader>(provider => provider.GetRequiredService<THandler>().InvokeAsync);

    // TReader has to be registered by the user
    public static OptionsBuilder<QueueOptions> AddBackgroundQueueConsumer<T, TReader>(this IServiceCollection services,
        Action<BackgroundQueue<T>.Builder> configure) where TReader : IAsyncEnumerable<T> =>
        services.AddBackgroundQueueConsumer<T>(Reflection.FriendlyNameOf<T>(), builder => configure(
            builder.SetReaderFactory(provider => provider.GetRequiredService<TReader>())));

    public static OptionsBuilder<QueueOptions> AddBackgroundQueueConsumer<T>(this IServiceCollection services,
        Action<BackgroundQueue<T>.Builder> configure) =>
        services.AddBackgroundQueueConsumer(Reflection.FriendlyNameOf<T>(), configure);

    public static OptionsBuilder<QueueOptions> AddBackgroundQueueConsumer<T>(this IServiceCollection services,
        string name, Action<BackgroundQueue<T>.Builder> configure)
    {
        var builder = new BackgroundQueue<T>.Builder(name);
        configure(builder);

        // TODO Try...() version of this one, to be gentle with multiple registrations of the same queue
        //      (extend ServiceDescriptor, add name to it and search using it)
        services.AddHostedService(builder.Build);

        return services.AddOptions<QueueOptions>(name);
    }
}
