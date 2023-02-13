using System.ComponentModel.DataAnnotations;
using Confluent.Kafka;

namespace LocalPost.KafkaConsumer;

/// <summary>
///     General Azure Storage Queue consumer settings
/// </summary>
public sealed class ConsumerOptions : ConsumerConfig
{
    /// <summary>
    ///     How many messages to process in parallel.
    /// </summary>
    [Required] public ushort MaxConcurrency { get; set; } = 10;

    [Required] public string TopicName { get; set; } = null!;
}
