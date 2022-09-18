using Amazon.SimpleNotificationService.Model;
using LocalPost.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LocalPost.AmazonSns.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAmazonSnsBatchPublisher(this IServiceCollection services,
        ushort maxConcurrency = 10) => services
        .AddAmazonSnsBatchPublisher(provider => provider.GetRequiredService<Sender>().Send, maxConcurrency);

    public static IServiceCollection AddAmazonSnsBatchPublisher(this IServiceCollection services,
        Func<IServiceProvider, MessageHandler<PublishBatchRequest>> consumerFactory, ushort maxConcurrency = 10)
    {
        services.TryAddSingleton<Sender>();

        services.TryAddSingleton<Publisher>();
        services.TryAddSingleton<ISnsPublisher>(provider =>
            provider.GetRequiredService<Publisher>());

        return services.AddCustomBackgroundQueue<PublishBatchRequest, Publisher>(consumerFactory, maxConcurrency);
    }
}
