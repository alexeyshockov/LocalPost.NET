using Confluent.Kafka;

namespace LocalPost.KafkaConsumer;

public readonly record struct ConsumeContext<TKey, TValue>
{
    // To commit the offset manually, for example. Do we really need to support these complex use cases?..
    public required IConsumer<TKey, TValue> Client { get; init; }

    public required ConsumeResult<TKey, TValue> Result { get; init; }
}
