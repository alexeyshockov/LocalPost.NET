using JetBrains.Annotations;
using LocalPost.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LocalPost.SqsConsumer.DependencyInjection;

[PublicAPI]
public sealed class SqsBuilder(IServiceCollection services)
{
    public OptionsBuilder<EndpointOptions> Defaults { get; } = services.AddOptions<EndpointOptions>();

    /// <summary>
    ///     Default batch consumer pipeline.
    /// </summary>
    /// <param name="name">Consumer name (also the default queue name). Should be unique in the application.</param>
    /// <param name="hf">Handler factory.</param>
    /// <returns>Pipeline options builder.</returns>
    public OptionsBuilder<DefaultBatchPipelineOptions> AddBatchConsumer(string name,
        HandlerFactory<IEnumerable<ConsumeContext<string>>> hf)
    {
        var defaultPipeline = Pipeline
            .Create(hf, provider => provider.GetOptions<DefaultBatchPipelineOptions>(name))
            .Batch(provider => provider.GetOptions<DefaultBatchPipelineOptions>(name))
            .Buffer(provider => provider.GetOptions<DefaultBatchPipelineOptions>(name).Prefetch);

        Add(name, defaultPipeline)
            .Configure<IOptionsMonitor<DefaultBatchPipelineOptions>>((options, pipelineOptions) =>
                options.UpdateFrom(pipelineOptions.Get(name).Consume));

        return BatchedConsumerFor(name).Configure<IOptions<EndpointOptions>>((options, globalOptions) =>
        {
            options.Consume.UpdateFrom(globalOptions.Value);
            options.Consume.QueueName = name;
        });
    }

    /// <summary>
    ///     Default consumer pipeline.
    /// </summary>
    /// <param name="name">Consumer name (also the default queue name). Should be unique in the application.</param>
    /// <param name="hf">Handler factory.</param>
    /// <returns>Pipeline options builder.</returns>
    public OptionsBuilder<DefaultPipelineOptions> AddConsumer(string name, HandlerFactory<ConsumeContext<string>> hf)
    {
        var defaultPipeline = Pipeline
            .Create(hf, provider => provider.GetOptions<DefaultBatchPipelineOptions>(name))
            .Buffer(provider => provider.GetOptions<DefaultBatchPipelineOptions>(name).Prefetch);

        Add(name, defaultPipeline)
            .Configure<IOptionsMonitor<DefaultPipelineOptions>>((options, pipelineOptions) =>
                options.UpdateFrom(pipelineOptions.Get(name).Consume));

        return ConsumerFor(name).Configure<IOptions<EndpointOptions>>((options, globalOptions) =>
        {
            options.Consume.UpdateFrom(globalOptions.Value);
            options.Consume.QueueName = name;
        });
    }

    /// <summary>
    ///     Custom consumer pipeline.
    /// </summary>
    /// <param name="name">Consumer name (also the default queue name). Should be unique in the application.</param>
    /// <param name="pr">Pipeline registration.</param>
    /// <returns>Consumer options builder.</returns>
    internal OptionsBuilder<ConsumerOptions> Add(string name, PipelineRegistration<ConsumeContext<string>> pr)
    {
        if (string.IsNullOrEmpty(name))
            throw new ArgumentException("A proper (non empty) name is required", nameof(name));

        if (!services.TryAddQueueClient(name))
            // return ob; // Already added, don't register twice
            throw new InvalidOperationException($"SQS consumer {name} is already registered");

        services.TryAddNamedSingleton(name, provider => new MessageSource(
            provider.GetRequiredService<QueueClient>(name)
        ));
        services.AddBackgroundService<MessageSource>(name);

        var context = services.RegistrationContextFor<MessageSource>(name);
        pr(context, provider => provider.GetRequiredService<MessageSource>(name));

        return PipelineFor(name).Configure<IOptions<EndpointOptions>>((options, globalOptions) =>
        {
            options.UpdateFrom(globalOptions.Value);
            options.QueueName = name;
        });
    }

    public OptionsBuilder<ConsumerOptions> PipelineFor(string name) => services.AddOptions<ConsumerOptions>(name);

    public OptionsBuilder<DefaultPipelineOptions> ConsumerFor(string name) =>
        services.AddOptions<DefaultPipelineOptions>(name);

    public OptionsBuilder<DefaultBatchPipelineOptions> BatchedConsumerFor(string name) =>
        services.AddOptions<DefaultBatchPipelineOptions>(name);

    // TODO Health checks



    // public OptionsBuilder<BatchedOptions> AddBatchConsumer(string name, HandlerFactory<BatchConsumeContext<string>> hf)
    // {
    //     if (string.IsNullOrEmpty(name))
    //         throw new ArgumentException("A proper (non empty) name is required", nameof(name));
    //
    //     if (!services.TryAddQueueClient<BatchedOptions>(name))
    //         // return ob; // Already added, don't register twice
    //         throw new InvalidOperationException("SQS consumer is already registered");
    //
    //     services.TryAddNamedSingleton(name, provider =>
    //     {
    //         var options = provider.GetOptions<BatchedOptions>(name);
    //
    //         return new BatchMessageSource(provider.GetRequiredService<QueueClient>(name),
    //             ConsumeContext.BatchBuilder(
    //                 options.BatchMaxSize, TimeSpan.FromMilliseconds(options.BatchTimeWindowMilliseconds)));
    //     });
    //     services.AddBackgroundService<BatchMessageSource>(name);
    //
    //     // services.TryAddConsumerGroup<BatchConsumeContext<string>, BatchMessageSource>(name, hf,
    //     //     ConsumerOptions.From<BatchedOptions>(o => new ConsumerOptions(o.MaxConcurrency, o.BreakOnException)));
    //     services.TryAddBackgroundConsumer<BatchConsumeContext<string>, BatchMessageSource>(name, hf, provider =>
    //     {
    //         var options = provider.GetOptions<BatchedOptions>(name);
    //         return new ConsumerOptions(options.MaxConcurrency, options.BreakOnException);
    //     });
    //
    //     return services.AddOptions<BatchedOptions>(name).Configure<IOptions<EndpointOptions>>((options, commonConfig) =>
    //     {
    //         options.UpdateFrom(commonConfig.Value);
    //         options.QueueName = name;
    //     });
    // }
}
