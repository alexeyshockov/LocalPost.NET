using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace LocalPost.DependencyInjection;

public static class QueueRegistrationExtensions
{
    // THandler has to be registered by the user
    public static OptionsBuilder<QueueOptions> AddBackgroundQueue<T, THandler>(this IServiceCollection services)
        where THandler : IMessageHandler<T> =>
        services.AddBackgroundQueue<T>(provider => provider.GetRequiredService<THandler>().Process);

    public static OptionsBuilder<QueueOptions> AddBackgroundQueue<T>(this IServiceCollection services,
        Func<IServiceProvider, MessageHandler<T>> handlerFactory)
    {
        services.TryAddSingleton<BackgroundQueue<T>>();
        services.TryAddSingleton<IBackgroundQueue<T>>(provider => provider.GetRequiredService<BackgroundQueue<T>>());

        return services.AddCustomBackgroundQueue<T, BackgroundQueue<T>>(handlerFactory);
    }
}
