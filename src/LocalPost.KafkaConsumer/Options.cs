using System.ComponentModel.DataAnnotations;
using Confluent.Kafka;

namespace LocalPost.KafkaConsumer;

public sealed class Options
{
    public ConsumerConfig Kafka { get; set; } = new();

    [Required] public string TopicName { get; set; } = null!;

    public ConsumerOptions Consumer { get; set; } = new();
}
