using Confluent.Kafka;

namespace LocalPost.KafkaConsumer;

public readonly record struct ConsumeContext<TKey, TValue>
{
    // To commit the offset manually, we need something. But it's a complex case... For the future.
//    public required IConsumer<TKey, TValue> Client { get; init; }

    public required ConsumeResult<TKey, TValue> Result { get; init; }
}
