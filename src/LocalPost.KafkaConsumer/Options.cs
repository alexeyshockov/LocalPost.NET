using System.ComponentModel.DataAnnotations;
using Confluent.Kafka;

namespace LocalPost.KafkaConsumer;

public sealed record ConsumerOptions
{
    public ConsumerConfig ClientConfig { get; set; } = new();
    // public required ConsumerConfig ClientConfig { get; init; } = new()
    // {
    //     EnableAutoOffsetStore = false // We will store offsets manually, see Acknowledge middleware
    // };

    [MinLength(1)]
    public ISet<string> Topics { get; set; } = new HashSet<string>();

    [Range(1, ushort.MaxValue)]
    public ushort Consumers { get; set; } = 1;

    internal void EnrichFrom(Config config)
    {
        foreach (var kv in config)
            ClientConfig.Set(kv.Key, kv.Value);
    }
}
