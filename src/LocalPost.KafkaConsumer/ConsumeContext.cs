using System.Diagnostics.CodeAnalysis;
using Confluent.Kafka;
using JetBrains.Annotations;
using LocalPost.AsyncEnumerable;

namespace LocalPost.KafkaConsumer;

internal static class ConsumeContext
{
    public static BatchBuilderFactory<ConsumeContext<byte[]>, BatchConsumeContext<byte[]>> BatchBuilder(
        BatchSize batchMaxSizeSize, TimeSpan timeWindow) => ct =>
        new BatchConsumeContext<byte[]>.Builder(batchMaxSizeSize, timeWindow, ct);
}

[PublicAPI]
public readonly record struct ConsumeContext<T>
{
    internal readonly KafkaTopicClient Client;
    internal readonly TopicPartitionOffset Offset;
    internal readonly Message<Ignore, byte[]> Message;
    public readonly T Payload;

    internal ConsumeContext(KafkaTopicClient client, TopicPartitionOffset offset, Message<Ignore, byte[]> message,
        T payload)
    {
        Client = client;
        Offset = offset;
        Message = message;
        Payload = payload;
    }

    public string Topic => Client.Topic;

    public IReadOnlyList<IHeader> Headers => Message.Headers.BackingList;

    public ConsumeContext<TOut> Transform<TOut>(TOut payload) => new(Client, Offset, Message, payload);

    public static implicit operator T(ConsumeContext<T> context) => context.Payload;

    public void Deconstruct(out T payload, out IReadOnlyList<IHeader> headers)
    {
        payload = Payload;
        headers = Headers;
    }
}

[PublicAPI]
public readonly record struct BatchConsumeContext<T>
{
    internal sealed class Builder :
        BoundedBatchBuilderBase<ConsumeContext<T>, BatchConsumeContext<T>>
    {
        public Builder(BatchSize batchMaxSizeSize, TimeSpan timeWindow, CancellationToken ct = default) :
            base(batchMaxSizeSize, timeWindow, ct)
        {
        }

        public override BatchConsumeContext<T> Build() => new(Batch);
    }

    public readonly IReadOnlyList<ConsumeContext<T>> Messages;

    internal BatchConsumeContext(IReadOnlyList<ConsumeContext<T>> messages)
    {
        if (messages.Count == 0)
            throw new ArgumentException("Batch must contain at least one message", nameof(messages));

        Messages = messages;
    }

    public BatchConsumeContext<TOut> Transform<TOut>(ConsumeContext<TOut>[] payload) => new(payload);

    public BatchConsumeContext<TOut> Transform<TOut>(IEnumerable<ConsumeContext<TOut>> payload) =>
        Transform(payload.ToArray());

    internal KafkaTopicClient Client => Messages[^1].Client;

    // Use .MaxBy() to not rely on the order?..
    internal TopicPartitionOffset LatestOffset => Messages[^1].Offset;
}
