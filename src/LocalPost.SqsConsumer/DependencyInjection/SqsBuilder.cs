using JetBrains.Annotations;
using LocalPost.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LocalPost.SqsConsumer.DependencyInjection;

[PublicAPI]
public sealed class SqsBuilder(IServiceCollection services)
{
    // public IServiceCollection Services { get; }

    public OptionsBuilder<EndpointOptions> Defaults { get; } = services.AddOptions<EndpointOptions>();

    public OptionsBuilder<Options> AddConsumer(string name, HandlerFactory<ConsumeContext<string>> hf)
    {
        if (string.IsNullOrEmpty(name))
            throw new ArgumentException("A proper (non empty) name is required", nameof(name));

        if (!services.TryAddQueueClient<Options>(name))
            // return ob; // Already added, don't register twice
            throw new InvalidOperationException("SQS consumer is already registered");

        services.TryAddNamedSingleton(name, provider => new MessageSource(
            provider.GetRequiredService<QueueClient>(name),
            provider.GetOptions<Options>(name).Prefetch
        ));
        services.AddBackgroundServiceForNamed<MessageSource>(name);

        services.TryAddBackgroundConsumer<ConsumeContext<string>, MessageSource>(name, hf, provider =>
        {
            var options = provider.GetOptions<Options>(name);
            return new ConsumerOptions(options.MaxConcurrency, options.BreakOnException);
        });

        return services.AddOptions<Options>(name).Configure<IOptions<EndpointOptions>>((options, commonConfig) =>
        {
            options.UpdateFrom(commonConfig.Value);
            options.QueueName = name;
        });
    }

    public OptionsBuilder<BatchedOptions> AddBatchConsumer(string name, HandlerFactory<BatchConsumeContext<string>> hf)
    {
        if (string.IsNullOrEmpty(name))
            throw new ArgumentException("A proper (non empty) name is required", nameof(name));

        if (!services.TryAddQueueClient<BatchedOptions>(name))
            // return ob; // Already added, don't register twice
            throw new InvalidOperationException("SQS consumer is already registered");

        services.TryAddNamedSingleton(name, provider =>
        {
            var options = provider.GetOptions<BatchedOptions>(name);

            return new BatchMessageSource(provider.GetRequiredService<QueueClient>(name),
                ConsumeContext.BatchBuilder(
                    options.BatchMaxSize, TimeSpan.FromMilliseconds(options.BatchTimeWindowMilliseconds)));
        });
        services.AddBackgroundServiceForNamed<BatchMessageSource>(name);

        // services.TryAddConsumerGroup<BatchConsumeContext<string>, BatchMessageSource>(name, hf,
        //     ConsumerOptions.From<BatchedOptions>(o => new ConsumerOptions(o.MaxConcurrency, o.BreakOnException)));
        services.TryAddBackgroundConsumer<BatchConsumeContext<string>, BatchMessageSource>(name, hf, provider =>
        {
            var options = provider.GetOptions<BatchedOptions>(name);
            return new ConsumerOptions(options.MaxConcurrency, options.BreakOnException);
        });

        return services.AddOptions<BatchedOptions>(name).Configure<IOptions<EndpointOptions>>((options, commonConfig) =>
        {
            options.UpdateFrom(commonConfig.Value);
            options.QueueName = name;
        });
    }
}
