using System.Collections.Immutable;
using Confluent.Kafka;
using JetBrains.Annotations;
using LocalPost.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LocalPost.KafkaConsumer.DependencyInjection;

[PublicAPI]
public sealed class KafkaBuilder(IServiceCollection services)
{
    public OptionsBuilder<ClientConfig> Defaults { get; } = services.AddOptions<ClientConfig>();

    /// <summary>
    ///     Default batch consumer pipeline.
    /// </summary>
    /// <param name="name">Consumer name (also the default queue name). Should be unique in the application.</param>
    /// <param name="hf">Handler factory.</param>
    /// <returns>Pipeline options builder.</returns>
    public OptionsBuilder<DefaultBatchPipelineOptions> AddBatchConsumer(string name,
        HandlerFactory<ImmutableArray<ConsumeContext<byte[]>>> hf)
    {
        var defaultPipeline = Pipeline
            .Create(hf, provider => provider.GetOptions<DefaultBatchPipelineOptions>(name))
            .Batch(provider => provider.GetOptions<DefaultBatchPipelineOptions>(name));

        Add(name, defaultPipeline)
            .Configure<IOptionsMonitor<DefaultBatchPipelineOptions>>((options, pipelineOptions) =>
                options.UpdateFrom(pipelineOptions.Get(name).Consume));

        return BatchedConsumerFor(name).Configure<IOptions<ClientConfig>>((options, clientConfig) =>
        {
            options.Consume.EnrichFrom(clientConfig.Value);
            options.Consume.Topic = name;
        });
    }

    /// <summary>
    ///     Default consumer pipeline.
    /// </summary>
    /// <param name="name">Consumer name (also the default queue name). Should be unique in the application.</param>
    /// <param name="hf">Handler factory.</param>
    /// <returns>Pipeline options builder.</returns>
    public OptionsBuilder<DefaultPipelineOptions> AddConsumer(string name, HandlerFactory<ConsumeContext<byte[]>> hf)
    {
        var defaultPipeline = Pipeline
            .Create(hf, provider => provider.GetOptions<DefaultPipelineOptions>(name));

        Add(name, defaultPipeline)
            .Configure<IOptionsMonitor<DefaultPipelineOptions>>((options, pipelineOptions) =>
                options.UpdateFrom(pipelineOptions.Get(name).Consume));

        return ConsumerFor(name).Configure<IOptions<ClientConfig>>((options, clientConfig) =>
        {
            options.Consume.EnrichFrom(clientConfig.Value);
            options.Consume.Topic = name;
        });
    }

    internal OptionsBuilder<ConsumerOptions> Add(string name, PipelineRegistration<ConsumeContext<byte[]>> pr)
    {
        if (string.IsNullOrEmpty(name)) // TODO Just default (empty?) name...
            throw new ArgumentException("A proper (non empty) name is required", nameof(name));

        if (!services.TryAddKafkaClient(name))
            throw new ArgumentException("Kafka consumer is already registered", nameof(name));

        services.TryAddNamedSingleton(name, provider =>
            new MessageSource(provider.GetRequiredService<KafkaTopicClient>(name)));
        services.AddBackgroundService<MessageSource>(name);

        pr(services.RegistrationContextFor<MessageSource>(name), provider =>
            provider.GetRequiredService<MessageSource>(name));

        return PipelineFor(name).Configure<IOptions<ClientConfig>>((options, clientConfig) =>
        {
            options.EnrichFrom(clientConfig.Value);
            options.Topic = name;
        });
    }

    public OptionsBuilder<ConsumerOptions> PipelineFor(string name) => services.AddOptions<ConsumerOptions>(name);

    public OptionsBuilder<DefaultPipelineOptions> ConsumerFor(string name) =>
        services.AddOptions<DefaultPipelineOptions>(name);

    public OptionsBuilder<DefaultBatchPipelineOptions> BatchedConsumerFor(string name) =>
        services.AddOptions<DefaultBatchPipelineOptions>(name);

    // TODO Health checks



    // public OptionsBuilder<BatchedOptions> AddBatchConsumer(string name,
    //     HandlerFactory<BatchConsumeContext<byte[]>> configure)
    // {
    //     if (string.IsNullOrEmpty(name))
    //         throw new ArgumentException("A proper (non empty) name is required", nameof(name));
    //
    //     if (!services.TryAddKafkaClient<BatchedOptions>(name))
    //         throw new InvalidOperationException("Kafka consumer is already registered");
    //
    //     services.TryAddNamedSingleton(name, provider =>
    //     {
    //         var options = provider.GetOptions<BatchedOptions>(name);
    //
    //         return new BatchMessageSource(provider.GetRequiredService<KafkaTopicClient>(name),
    //             ConsumeContext.BatchBuilder(
    //                 options.BatchMaxSize, TimeSpan.FromMilliseconds(options.BatchTimeWindowMilliseconds)));
    //     });
    //     services.AddBackgroundService<BatchMessageSource>(name);
    //
    //     services.TryAddBackgroundConsumer<BatchConsumeContext<byte[]>, BatchMessageSource>(name, configure, provider =>
    //     {
    //         var options = provider.GetOptions<BatchedOptions>(name);
    //         return new ConsumerOptions(1, options.BreakOnException);
    //     });
    //
    //     return services.AddOptions<BatchedOptions>(name).Configure<IOptions<ClientConfig>>((options, commonConfig) =>
    //     {
    //         options.EnrichFrom(commonConfig.Value);
    //         options.Topic = name;
    //     });
    // }
}
