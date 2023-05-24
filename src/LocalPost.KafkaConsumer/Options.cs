using System.ComponentModel.DataAnnotations;
using Confluent.Kafka;

namespace LocalPost.KafkaConsumer;

public sealed class Options
{
    public ConsumerConfig Kafka { get; set; } = new();

    [Required] public string TopicName { get; set; } = null!;

    /// <summary>
    ///     How many messages to process in parallel.
    /// </summary>
    [Required] public ushort MaxConcurrency { get; set; } = ushort.MaxValue;
}
