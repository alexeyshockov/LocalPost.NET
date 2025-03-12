using Amazon.SQS;
using LocalPost.DependencyInjection;
using LocalPost.Flow;
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
    /// <param name="hmf">Message handler factory.</param>
    /// <returns>Consumer options builder.</returns>
    public OptionsBuilder<ConsumerOptions> AddConsumer(HandlerManagerFactory<ConsumeContext<string>> hmf) =>
        AddConsumer(Options.DefaultName, hmf);

    /// <summary>
    ///     Add an SQS consumer with a custom message handler.
    /// </summary>
    /// <param name="name">Consumer name (should be unique in the application). Also, the default queue name.</param>
    /// <param name="hmf">Message handler factory.</param>
    /// <returns>Consumer options builder.</returns>
    public OptionsBuilder<ConsumerOptions> AddConsumer(string name, HandlerManagerFactory<ConsumeContext<string>> hmf)
    {
        var added = services.TryAddKeyedSingleton(name, (provider, _) => new Consumer(name,
            provider.GetLoggerFor<Consumer>(),
            provider.GetRequiredService<IAmazonSQS>(),
            provider.GetOptions<ConsumerOptions>(name),
            hmf(provider)
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
