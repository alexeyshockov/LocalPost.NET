using System.ComponentModel.DataAnnotations;
using Confluent.Kafka;
using JetBrains.Annotations;

namespace LocalPost.KafkaConsumer;

[PublicAPI]
public class Options : ConsumerConfig
{
    public Options()
    {
        EnableAutoOffsetStore = false; // We will store offsets manually, see Acknowledge middleware
    }

    [Required] public string Topic { get; set; } = null!;

    /// <summary>
    ///     Stop the consumer in case of an exception in the handler, or just log it and continue the processing loop.
    ///     Default is true.
    /// </summary>
    public bool BreakOnException { get; set; } = true;

    internal void EnrichFrom(Config config)
    {
        foreach (var kv in config)
            Set(kv.Key, kv.Value);
    }
}

[PublicAPI]
public class BatchedOptions : Options
{
    [Range(1, ushort.MaxValue)] public ushort BatchMaxSize { get; set; } = 100;

    [Range(1, ushort.MaxValue)] public int BatchTimeWindowMilliseconds { get; set; } = 1_000;
}
