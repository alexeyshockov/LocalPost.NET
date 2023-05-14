using Amazon.SimpleNotificationService.Model;
using LocalPost.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace LocalPost.SnsPublisher.DependencyInjection;

public static class ServiceRegistration
{
    public static OptionsBuilder<PublisherOptions> AddAmazonSnsBatchPublisher(this IServiceCollection services)
    {
        services.TryAddSingleton<Sender>();

        return services.AddAmazonSnsBatchPublisher(builder =>
            builder.MiddlewareStackBuilder.SetHandler<Sender>());
    }

    public static OptionsBuilder<PublisherOptions> AddAmazonSnsBatchPublisher(this IServiceCollection services,
        Action<BackgroundQueue<PublishBatchRequest>.ConsumerBuilder> configure)
    {
        services.TryAddSingleton<Publisher>();
        services.TryAddSingleton<ISnsPublisher>(provider => provider.GetRequiredService<Publisher>());

        services
            .AddBackgroundQueueConsumer<PublishBatchRequest, Publisher>(configure)
            .Configure<IOptions<PublisherOptions>>(
                (options, consumerOptions) => { options.MaxConcurrency = consumerOptions.Value.Sender.MaxConcurrency; });

        return services.AddOptions<PublisherOptions>();


    }
}
