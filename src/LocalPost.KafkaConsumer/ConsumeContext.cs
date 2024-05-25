using Confluent.Kafka;
using JetBrains.Annotations;
using LocalPost.AsyncEnumerable;

namespace LocalPost.KafkaConsumer;

internal static class ConsumeContext
{
    public static BatchBuilderFactory<ConsumeContext<byte[]>, BatchConsumeContext<byte[]>> BatchBuilder(
        MaxSize batchMaxSize, TimeSpan timeWindow) => ct =>
        new BatchConsumeContext<byte[]>.Builder(batchMaxSize, timeWindow, ct);
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

    public void Deconstruct(out T payload, out IReadOnlyList<IHeader> headers)
    {
        payload = Payload;
        headers = Headers;
    }

    public string Topic => Client.Topic;

    public IReadOnlyList<IHeader> Headers => Message.Headers.BackingList;

    public ConsumeContext<TOut> Transform<TOut>(TOut payload) => new(Client, Offset, Message, payload);

    public ConsumeContext<TOut> Transform<TOut>(Func<ConsumeContext<T>, TOut> transform) => Transform(transform(this));

    public async Task<ConsumeContext<TOut>> Transform<TOut>(Func<ConsumeContext<T>, Task<TOut>> transform) =>
        Transform(await transform(this));

    public static implicit operator T(ConsumeContext<T> context) => context.Payload;
}

[PublicAPI]
public readonly record struct BatchConsumeContext<T>
{
    internal sealed class Builder(MaxSize batchMaxSize, TimeSpan timeWindow, CancellationToken ct = default)
        : BoundedBatchBuilderBase<ConsumeContext<T>, BatchConsumeContext<T>>(batchMaxSize, timeWindow, ct)
    {
        public override BatchConsumeContext<T> Build()
        {
// #if NET6_0_OR_GREATER
//             ReadOnlySpan<T> s = CollectionsMarshal.AsSpan(Batch)
//             var ia = s.ToImmutableArray();
//             return new BatchConsumeContext<T>(Batch);
// #else
//             return new BatchConsumeContext<T>(Batch.ToImmutableArray());
// #endif
             return new BatchConsumeContext<T>(Batch);
        }
    }

    // TODO ImmutableArray
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

    public BatchConsumeContext<TOut> Transform<TOut>(IEnumerable<TOut> batchPayload) =>
        Transform(Messages.Zip(batchPayload, (message, payload) => message.Transform(payload)));

    public BatchConsumeContext<TOut> Transform<TOut>(Func<ConsumeContext<T>, TOut> transform)
    {
        // TODO Parallel LINQ
        var messages = Messages.Select(transform);
        return Transform(messages);
    }

    public async Task<BatchConsumeContext<TOut>> Transform<TOut>(Func<ConsumeContext<T>, Task<TOut>> transform)
    {
        var messages = await Task.WhenAll(Messages.Select(transform));
        return Transform(messages);
    }

    internal KafkaTopicClient Client => Messages[^1].Client;

    // Use .MaxBy() to not rely on the order?..
    internal TopicPartitionOffset LatestOffset => Messages[^1].Offset;
}
