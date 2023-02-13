using LocalPost.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LocalPost.KafkaConsumer.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static OptionsBuilder<ConsumerOptions> AddKafkaConsumer<TKey, TValue>(this IServiceCollection services,
        string name, Action<Consumer<TKey, TValue>.Builder> configure)
    {
        var builder = new Consumer<TKey, TValue>.Builder { Name = name };
        configure(builder);

        services.AddHostedService(provider => builder.Build(provider));
        services
            .AddCustomBackgroundQueue($"Kafka/{name}", _ => builder.Messages, builder.BuildHandlerFactory())
            .Configure<IOptionsMonitor<ConsumerOptions>>(
                (options, consumerOptions) => { options.MaxConcurrency = consumerOptions.Get(name).MaxConcurrency; });

        // TODO Health check, metrics (with all topics for this consumer... (it can more than 1))

        return services.AddOptions<ConsumerOptions>(name).Configure(options => options.TopicName = name);
    }
}
