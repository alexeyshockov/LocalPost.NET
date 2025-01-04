using Confluent.Kafka;

namespace LocalPost.KafkaConsumer;

[PublicAPI]
public readonly record struct ConsumeContext<T>
{
    // librdkafka docs:
    //   When consumer restarts this is where it will start consuming from.
    //   The committed offset should be last_message_offset+1.
    // See https://github.com/confluentinc/librdkafka/wiki/Consumer-offset-management#terminology
//    internal readonly TopicPartitionOffset NextOffset;

    internal readonly Client Client;
    internal readonly ConsumeResult<Ignore, byte[]> ConsumeResult;
    public readonly T Payload;

    internal ConsumeContext(Client client, ConsumeResult<Ignore, byte[]> consumeResult, T payload)
    {
        Client = client;
        ConsumeResult = consumeResult;
        Payload = payload;
    }

    public void Deconstruct(out T payload, out IReadOnlyList<IHeader> headers)
    {
        payload = Payload;
        headers = Headers;
    }

    public Offset NextOffset => ConsumeResult.Offset + 1;

    public Message<Ignore, byte[]> Message => ConsumeResult.Message;

    public string Topic => ConsumeResult.Topic;

    public IReadOnlyList<IHeader> Headers => Message.Headers.BackingList;

    public ConsumeContext<TOut> Transform<TOut>(TOut payload) => new(Client, ConsumeResult, payload);

    public ConsumeContext<TOut> Transform<TOut>(Func<ConsumeContext<T>, TOut> transform) => Transform(transform(this));

    public async Task<ConsumeContext<TOut>> Transform<TOut>(Func<ConsumeContext<T>, Task<TOut>> transform) =>
        Transform(await transform(this));

    public static implicit operator T(ConsumeContext<T> context) => context.Payload;

    public void StoreOffset() => Client.Consumer.StoreOffset(ConsumeResult);

    // To be consistent across different message brokers
    public void Acknowledge() => StoreOffset();
}
