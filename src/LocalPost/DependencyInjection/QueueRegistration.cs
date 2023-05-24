using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace LocalPost.DependencyInjection;

public static class QueueRegistration
{
    // THandler has to be registered by the user
    public static OptionsBuilder<BackgroundQueueOptions<T>> AddBackgroundQueue<T, THandler>(this IServiceCollection services,
        Action<MiddlewareStackBuilder<T>>? configure = null) where THandler : IHandler<T> =>
        services.AddBackgroundQueue<T>(builder =>
        {
            builder.SetHandler<THandler>();
            configure?.Invoke(builder);
        });

    public static OptionsBuilder<BackgroundQueueOptions<T>> AddBackgroundQueue<T>(this IServiceCollection services,
        Handler<T> handler, Action<MiddlewareStackBuilder<T>>? configure = null) =>
        services.AddBackgroundQueue<T>(builder =>
        {
            builder.SetHandler(handler);
            configure?.Invoke(builder);
        });

    public static OptionsBuilder<BackgroundQueueOptions<T>> AddBackgroundQueue<T>(this IServiceCollection services,
        Action<MiddlewareStackBuilder<T>> configure)
    {
        services.TryAddConcurrentHostedServices();

        var handleStackBuilder = new MiddlewareStackBuilder<T>();
        configure(handleStackBuilder);
        var handlerStack = handleStackBuilder.Build();

        services.TryAddSingleton(provider => BackgroundQueueService<T>.Create(provider, handlerStack));

        services.TryAddSingleton<IBackgroundQueue<T>>(provider =>
            provider.GetRequiredService<BackgroundQueueService<T>>().Queue);

        services.AddSingleton<IConcurrentHostedService>(provider =>
            provider.GetRequiredService<BackgroundQueueService<T>>().QueueSupervisor);
        services.AddSingleton<IConcurrentHostedService>(provider =>
            provider.GetRequiredService<BackgroundQueueService<T>>().ConsumerGroup);

        // Extend ServiceDescriptor for better comparison and implement custom TryAddSingleton later...

        return services.AddOptions<BackgroundQueueOptions<T>>();
    }

    // TODO Batched queue consumer
}
