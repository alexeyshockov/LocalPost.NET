using LocalPost.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LocalPost.KafkaConsumer.DependencyInjection;

public static class ServiceRegistration
{
    public static OptionsBuilder<Options> AddKafkaConsumer<TKey, TValue>(this IServiceCollection services,
        string name, Action<MessageSource<TKey, TValue>.Builder> configure)
    {
        var builder = new MessageSource<TKey, TValue>.Builder(name);
        configure(builder);
        services.AddHostedService(builder.Build);

        services
            .AddBackgroundQueueConsumer<ConsumeContext<TKey, TValue>>(name, b => b
                .SetReaderFactory(_ => builder.Messages)
                .MiddlewareStackBuilder.SetHandler(builder.BuildHandlerFactory()))
            .Configure<IOptionsMonitor<Options>>(
                (options, consumerOptions) => { options.MaxConcurrency = consumerOptions.Get(name).Consumer.MaxConcurrency; });

        return services.AddOptions<Options>(name).Configure(options => options.TopicName = name);
    }
}
