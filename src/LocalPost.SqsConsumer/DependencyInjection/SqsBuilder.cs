using Amazon.SQS;
using LocalPost.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LocalPost.SqsConsumer.DependencyInjection;

[PublicAPI]
public sealed class SqsBuilder(IServiceCollection services)
{
    public OptionsBuilder<EndpointOptions> Defaults { get; } = services.AddOptions<EndpointOptions>();

    /// <summary>
    ///     Add an SQS consumer with a custom message handler.
    /// </summary>
    /// <param name="name">Consumer name (should be unique in the application). Also, the default queue name.</param>
    /// <param name="hf">Message handler factory.</param>
    /// <returns>Consumer options builder.</returns>
    public OptionsBuilder<ConsumerOptions> AddConsumer(string name, HandlerFactory<ConsumeContext<string>> hf)
    {
        if (string.IsNullOrEmpty(name)) // TODO Just default (empty?) name...
            throw new ArgumentException("A proper (non empty) name is required", nameof(name));

        var added = services.TryAddKeyedSingleton(name, (provider, _) => new Consumer(name,
            provider.GetLoggerFor<Consumer>(),
            provider.GetRequiredService<IAmazonSQS>(),
            provider.GetOptions<ConsumerOptions>(name),
            hf(provider)
        ));

        if (!added)
            throw new ArgumentException("Consumer is already registered", nameof(name));

        services.AddHostedService(provider => provider.GetRequiredKeyedService<Consumer>(name));

        return OptionsFor(name).Configure<IOptions<EndpointOptions>>((co, defaults) =>
        {
            co.UpdateFrom(defaults.Value);
            co.QueueName = name;
        });
    }

    public OptionsBuilder<ConsumerOptions> OptionsFor(string name) => services.AddOptions<ConsumerOptions>(name);
}
