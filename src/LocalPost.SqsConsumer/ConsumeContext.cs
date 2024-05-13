using System.Collections.Immutable;
using Amazon.SQS.Model;
using JetBrains.Annotations;
using LocalPost.AsyncEnumerable;

namespace LocalPost.SqsConsumer;

internal static class ConsumeContext
{
    public static BatchBuilderFactory<ConsumeContext<string>, BatchConsumeContext<string>> BatchBuilder(
        BatchSize batchMaxSizeSize, TimeSpan timeWindow) => ct =>
        new BatchConsumeContext<string>.Builder(batchMaxSizeSize, timeWindow, ct);
}

[PublicAPI]
public readonly record struct ConsumeContext<T>
{
    internal readonly QueueClient Client;
    internal readonly Message Message;
    public readonly T Payload;

    public DateTimeOffset ReceivedAt { get; init; } = DateTimeOffset.Now;

    internal ConsumeContext(QueueClient client, Message message, T payload)
    {
        Client = client;
        Payload = payload;
        Message = message;
    }

    // TODO Headers instead of the message
    public void Deconstruct(out T payload, out Message message)
    {
        payload = Payload;
        message = Message;
    }

    public string MessageId => Message.MessageId;

    public string ReceiptHandle => Message.ReceiptHandle;

    public IReadOnlyDictionary<string, string> Attributes => Message.Attributes;

    public IReadOnlyDictionary<string, MessageAttributeValue> MessageAttributes => Message.MessageAttributes;

    public bool IsStale => false; // TODO Check the visibility timeout

    public ConsumeContext<TOut> Transform<TOut>(TOut payload) =>
        new(Client, Message, payload)
        {
            ReceivedAt = ReceivedAt
        };

    public ConsumeContext<TOut> Transform<TOut>(Func<ConsumeContext<T>, TOut> transform) => Transform(transform(this));

    public async Task<ConsumeContext<TOut>> Transform<TOut>(Func<ConsumeContext<T>, Task<TOut>> transform) =>
        Transform(await transform(this));

    public static implicit operator T(ConsumeContext<T> context) => context.Payload;
}

[PublicAPI]
public readonly record struct BatchConsumeContext<T>
{
    internal sealed class Builder : BoundedBatchBuilderBase<ConsumeContext<T>, BatchConsumeContext<T>>
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

    internal QueueClient Client => Messages[0].Client;
}
