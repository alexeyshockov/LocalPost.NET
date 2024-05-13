using System.ComponentModel.DataAnnotations;
using Confluent.Kafka;
using JetBrains.Annotations;

namespace LocalPost.KafkaConsumer;

[PublicAPI]
public record Options
{
    /// <summary>
    ///     Group ID, auth and other options should be set directly.
    /// </summary>
    public ConsumerConfig Kafka { get; set; } = new()
    {
        EnableAutoOffsetStore = false, // We will store offsets manually, see ConsumeContext class
    };

    [Required] public string Topic { get; set; } = null!;

    // TODO Implement (via ApplicationLifecycle)
//    public bool ShutdownAppOnFatalError { get; set; } = true;

    // Implement later?..
//    /// <summary>
//    ///     How many (parallel) consumers to spawn.
//    /// </summary>
//    [Range(1, 10)]
//    public byte Instances { get; set; } = 1;
}

[PublicAPI]
public record BatchedOptions : Options
{
    [Range(1, ushort.MaxValue)]
    public ushort BatchMaxSize { get; set; } = 100;

    [Range(1, ushort.MaxValue)]
    public int BatchTimeWindowMilliseconds { get; set; } = 1_000;
}
