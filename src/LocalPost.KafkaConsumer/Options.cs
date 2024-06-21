using System.ComponentModel.DataAnnotations;
using Confluent.Kafka;
using JetBrains.Annotations;
using Microsoft.Extensions.Options;

namespace LocalPost.KafkaConsumer;

[PublicAPI]
public sealed class ConsumerOptions : ConsumerConfig
{
    public ConsumerOptions()
    {
        EnableAutoOffsetStore = false; // We will store offsets manually, see Acknowledge middleware
    }

    [Required]
    public string Topic { get; set; } = null!;

    internal void EnrichFrom(Config config)
    {
        foreach (var kv in config)
            Set(kv.Key, kv.Value);
    }

    internal void UpdateFrom(ConsumerOptions other)
    {
        EnrichFrom(other);
        Topic = other.Topic;
    }
}

[PublicAPI]
public sealed class DefaultPipelineOptions
{
    public void Deconstruct(out ConsumerOptions consumer, out DefaultPipelineOptions pipeline)
    {
        consumer = Consume;
        pipeline = this;
    }

    public ConsumerOptions Consume { get; } = new();

    [Range(1, ushort.MaxValue)]
    public ushort MaxConcurrency { get; set; } = 10;

    // [Range(1, ushort.MaxValue)]
    // public ushort Prefetch { get; set; } = 10;

    // /// <summary>
    // ///     Stop the consumer in case of an exception in the handler, or just log it and continue the processing loop.
    // ///     Default is true.
    // /// </summary>
    // public bool BreakOnException { get; set; } = true;

    public static implicit operator Pipeline.ConsumerOptions(DefaultPipelineOptions options) => new()
    {
        MaxConcurrency = options.MaxConcurrency,
        BreakOnException = false,
    };
}

[PublicAPI]
public sealed record DefaultBatchPipelineOptions
{
    public void Deconstruct(out ConsumerOptions consumer, out DefaultBatchPipelineOptions pipeline)
    {
        consumer = Consume;
        pipeline = this;
    }

    public ConsumerOptions Consume { get; } = new();

    [Range(1, ushort.MaxValue)]
    public ushort MaxConcurrency { get; set; } = 10;

    // [Range(1, ushort.MaxValue)]
    // public ushort Prefetch { get; set; } = 10;

    // /// <summary>
    // ///     Stop the consumer in case of an exception in the handler, or just log it and continue the processing loop.
    // ///     Default is true.
    // /// </summary>
    // public bool BreakOnException { get; set; } = true;

    [Range(1, ushort.MaxValue)]
    public ushort BatchMaxSize { get; set; } = 10;

    [Range(1, ushort.MaxValue)]
    public int TimeWindowMs { get; set; } = 1_000;

    public static implicit operator Pipeline.ConsumerOptions(DefaultBatchPipelineOptions options) => new()
    {
        MaxConcurrency = options.MaxConcurrency,
        BreakOnException = false,
    };

    public static implicit operator BatchOptions(DefaultBatchPipelineOptions options) => new()
    {
        MaxSize = options.BatchMaxSize,
        TimeWindowDuration = options.TimeWindowMs,
    };
}

[PublicAPI]
public static class OptionsBuilderEx
{
    public static OptionsBuilder<DefaultPipelineOptions> Configure(
        this OptionsBuilder<DefaultPipelineOptions> builder,
        Action<ConsumerOptions, DefaultPipelineOptions> configure) =>
        builder.Configure(options =>
        {
            var (consumer, pipeline) = options;

            configure(consumer, pipeline);
        });

    public static OptionsBuilder<DefaultPipelineOptions> ConfigureConsumer(
        this OptionsBuilder<DefaultPipelineOptions> builder,
        Action<ConsumerOptions> configure) =>
        builder.Configure(options =>
        {
            var (consumer, _) = options;

            configure(consumer);
        });

    public static OptionsBuilder<DefaultBatchPipelineOptions> Configure(
        this OptionsBuilder<DefaultBatchPipelineOptions> builder,
        Action<ConsumerOptions, DefaultBatchPipelineOptions> configure) =>
        builder.Configure(options =>
        {
            var (consumer, pipeline) = options;

            configure(consumer, pipeline);
        });

    public static OptionsBuilder<DefaultBatchPipelineOptions> ConfigureConsumer(
        this OptionsBuilder<DefaultBatchPipelineOptions> builder,
        Action<ConsumerOptions> configure) =>
        builder.Configure(options =>
        {
            var (consumer, _) = options;

            configure(consumer);
        });
}
