using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LocalPost.DependencyInjection;

public static class CustomQueueRegistrationExtensions
{
    // TReader & THandler have to be registered by the user
    public static OptionsBuilder<QueueOptions> AddCustomBackgroundQueue<T, TReader, THandler>(
        this IServiceCollection services) where TReader : IAsyncEnumerable<T> where THandler : IMessageHandler<T> =>
        services.AddCustomBackgroundQueue<T, TReader>(provider => provider.GetRequiredService<THandler>().Process);

    // TReader has to be registered by the user
    public static OptionsBuilder<QueueOptions> AddCustomBackgroundQueue<T, TReader>(this IServiceCollection services,
        Func<IServiceProvider, MessageHandler<T>> handlerFactory)
        where TReader : IAsyncEnumerable<T> =>
        services.AddCustomBackgroundQueue(Reflection.FriendlyNameOf<T>(),
            provider => provider.GetRequiredService<TReader>(), handlerFactory);

    public static OptionsBuilder<QueueOptions> AddCustomBackgroundQueue<T>(this IServiceCollection services, string name,
        Func<IServiceProvider, IAsyncEnumerable<T>> readerFactory,
        Func<IServiceProvider, MessageHandler<T>> handlerFactory)
    {
        // TODO Try...() version of this one, to be gentle with multiple registrations of the same queue?..
        services.AddHostedService(provider =>
        {
            var executor = ActivatorUtilities.CreateInstance<BoundedExecutor>(provider, name);

            return ActivatorUtilities.CreateInstance<BackgroundQueueConsumer<T>>(provider, name,
                readerFactory(provider), executor, handlerFactory);
        });

        // TODO Health check, metrics

        return services.AddOptions<QueueOptions>(name);;
    }
}
