using Confluent.Kafka;

namespace LocalPost.KafkaConsumer;

public readonly record struct ConsumeContext<TKey, TValue>
{
    public required IConsumer<TKey, TValue> Client { get; init; }

    public required ConsumeResult<TKey, TValue> Result { get; init; }
}
