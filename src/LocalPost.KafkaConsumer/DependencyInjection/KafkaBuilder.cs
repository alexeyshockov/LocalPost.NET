using Confluent.Kafka;
using LocalPost.DependencyInjection;
using LocalPost.Flow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LocalPost.KafkaConsumer.DependencyInjection;

[PublicAPI]
public sealed class KafkaBuilder(IServiceCollection services)
{
    public OptionsBuilder<ClientConfig> Defaults { get; } = services.AddOptions<ClientConfig>();

    /// <summary>
    ///     Add a Kafka consumer with a custom message handler.
    /// </summary>
    /// <param name="hf">Message handler factory.</param>
    /// <returns>Consumer options builder.</returns>
    public OptionsBuilder<ConsumerOptions> AddConsumer(HandlerFactory<ConsumeContext<byte[]>> hf) =>
        AddConsumer(Options.DefaultName, hf);

    /// <summary>
    ///     Add a Kafka consumer with a custom message handler.
    /// </summary>
    /// <param name="name">Consumer name (should be unique in the application). Also, the default group ID.</param>
    /// <param name="hf">Message handler factory.</param>
    /// <returns>Consumer options builder.</returns>
    public OptionsBuilder<ConsumerOptions> AddConsumer(string name, HandlerFactory<ConsumeContext<byte[]>> hf) =>
        AddConsumer(name, hf.SelectMessageEvent());

    /// <summary>
    ///     Add a Kafka consumer with a custom message handler.
    /// </summary>
    /// <param name="name">Consumer name (should be unique in the application). Also, the default group ID.</param>
    /// <param name="hf">Message handler factory.</param>
    /// <returns>Consumer options builder.</returns>
    public OptionsBuilder<ConsumerOptions> AddConsumer(string name, HandlerFactory<Event<ConsumeContext<byte[]>>> hf)
    {
        var added = services.TryAddKeyedSingleton(name, (provider, _) =>
        {
            var clientFactory = new ClientFactory(
                provider.GetLoggerFor<Client>(),
                provider.GetOptions<ConsumerOptions>(name)
            );

            return new Consumer(name,
                provider.GetLoggerFor<Consumer>(),
                clientFactory,
                hf(provider)
            );
        });

        if (!added)
            throw new ArgumentException("Consumer is already registered", nameof(name));

        services.AddHostedService(provider => provider.GetRequiredKeyedService<Consumer>(name));

        return OptionsFor(name).Configure<IOptions<ClientConfig>>((co, defaults) =>
        {
            co.EnrichFrom(defaults.Value);
            if (!string.IsNullOrEmpty(name))
                co.ClientConfig.GroupId = name;
        });
    }

    public OptionsBuilder<ConsumerOptions> OptionsFor(string name) => services.AddOptions<ConsumerOptions>(name);
}
