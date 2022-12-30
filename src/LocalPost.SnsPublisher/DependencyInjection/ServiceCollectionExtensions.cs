using Amazon.SimpleNotificationService.Model;
using LocalPost.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace LocalPost.SnsPublisher.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static OptionsBuilder<QueueOptions> AddAmazonSnsBatchPublisher(this IServiceCollection services)
    {
        services.TryAddSingleton<Sender>();

        return services
            .AddAmazonSnsBatchPublisher(provider => provider.GetRequiredService<Sender>().Send);
    }

    public static OptionsBuilder<QueueOptions> AddAmazonSnsBatchPublisher(this IServiceCollection services,
        Func<IServiceProvider, MessageHandler<PublishBatchRequest>> handlerFactory)
    {
        services.TryAddSingleton<Publisher>();
        services.TryAddSingleton<ISnsPublisher>(provider => provider.GetRequiredService<Publisher>());

        return services.AddCustomBackgroundQueue<PublishBatchRequest, Publisher>(handlerFactory);
    }
}
