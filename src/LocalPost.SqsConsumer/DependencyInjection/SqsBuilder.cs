using JetBrains.Annotations;
using LocalPost.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LocalPost.SqsConsumer.DependencyInjection;

[PublicAPI]
public sealed class SqsBuilder : OptionsBuilder<EndpointOptions>
{
    internal SqsBuilder(IServiceCollection services, string? name) : base(services, name)
    {
    }

    public OptionsBuilder<Options> AddConsumer(string name, HandlerFactory<ConsumeContext<string>> configure)
    {
        if (string.IsNullOrEmpty(name)) // TODO Just default empty name...
            throw new ArgumentException("A proper (non empty) name is required", nameof(name));

        // Services.AddSingleton<AcknowledgeMiddleware>();

        if (!Services.TryAddQueueClient<Options>(name))
            throw new ArgumentException("SQS consumer is already registered", nameof(name));

        Services.TryAddNamedSingleton(name, provider =>
            new MessageSource(provider.GetRequiredService<QueueClient>(name)));
        Services.AddBackgroundServiceForNamed<MessageSource>(name);

        Services.TryAddConsumerGroup<ConsumeContext<string>, MessageSource>(name, configure);

        return Services.AddOptions<Options>(name).Configure<IOptionsMonitor<EndpointOptions>>((options, commonConfigs) =>
        {
            var commonConfig = commonConfigs.Get(Name);

            options.UpdateFrom(commonConfig);
            options.QueueName = name;
        });
    }

    public OptionsBuilder<BatchedOptions> AddBatchConsumer(string name,
        HandlerFactory<BatchConsumeContext<string>> configure)
    {
        if (string.IsNullOrEmpty(name)) // TODO Just default empty name...
            throw new ArgumentException("A proper (non empty) name is required", nameof(name));

        // Services.AddSingleton<AcknowledgeMiddleware>();

        if (!Services.TryAddQueueClient<BatchedOptions>(name))
            throw new ArgumentException("SQS consumer is already registered", nameof(name));

        Services.TryAddNamedSingleton(name, provider =>
        {
            var options = provider.GetOptions<BatchedOptions>(name);

            return new BatchMessageSource(provider.GetRequiredService<QueueClient>(name),
                ConsumeContext.BatchBuilder(
                    options.BatchMaxSize, TimeSpan.FromMilliseconds(options.BatchTimeWindowMilliseconds)));
        });
        Services.AddBackgroundServiceForNamed<BatchMessageSource>(name);

        Services.TryAddConsumerGroup<BatchConsumeContext<string>, BatchMessageSource>(name, configure);
//        Services.TryAddConsumerGroup(name, provider => BackgroundQueue.ConsumerGroupFor(
//            provider.GetRequiredService<BatchMessageSource>(name), configure(provider), 1));

        return Services.AddOptions<BatchedOptions>(name).Configure<IOptionsMonitor<EndpointOptions>>((options, commonConfigs) =>
        {
            var commonConfig = commonConfigs.Get(Name);

            options.UpdateFrom(commonConfig);
            options.QueueName = name;
        });
    }
}
