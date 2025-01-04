using Confluent.Kafka;
using LocalPost.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LocalPost.KafkaConsumer.DependencyInjection;

[PublicAPI]
public sealed class KafkaBuilder(IServiceCollection services)
{
    public OptionsBuilder<ClientConfig> Defaults { get; } = services.AddOptions<ClientConfig>();

    /// <summary>
    ///     Add a Kafka consumer with <typeparamref name="THandler"/> (should be registered separately) as a message handler.
    /// </summary>
    /// <param name="name">Consumer name (should be unique in the application).</param>
    /// <typeparam name="THandler">Message handler type.</typeparam>
    /// <returns>Consumer options builder.</returns>
    public OptionsBuilder<ConsumerOptions> AddConsumer<THandler>(string name)
        where THandler : IHandler<ConsumeContext<byte[]>>
        => AddConsumer(name, provider => provider.GetRequiredService<THandler>().InvokeAsync);

    /// <summary>
    ///     Add a Kafka consumer with a custom message handler.
    /// </summary>
    /// <param name="name">Consumer name (should be unique in the application).</param>
    /// <param name="hf">Message handler factory.</param>
    /// <returns>Consumer options builder.</returns>
    public OptionsBuilder<ConsumerOptions> AddConsumer(string name, HandlerFactory<ConsumeContext<byte[]>> hf)
    {
        if (string.IsNullOrEmpty(name)) // TODO Just default (empty?) name...
            throw new ArgumentException("A proper (non empty) name is required", nameof(name));

        services.TryAddSingleton<ClientFactory>();

        var added = services.TryAddKeyedSingleton(name, (provider, _) => new Consumer(name,
            provider.GetLoggerFor<Consumer>(),
            provider.GetRequiredService<ClientFactory>(),
            provider.GetOptions<ConsumerOptions>(name),
            hf(provider)
        ));

        if (!added)
            throw new ArgumentException("Consumer is already registered", nameof(name));

        services.AddHostedService(provider => provider.GetRequiredKeyedService<Consumer>(name));

        return OptionsFor(name).Configure<IOptions<ClientConfig>>((co, defaults) => co.EnrichFrom(defaults.Value));
    }

    public OptionsBuilder<ConsumerOptions> OptionsFor(string name) => services.AddOptions<ConsumerOptions>(name);
}
