using Amazon.SQS.Model;

namespace LocalPost.SqsConsumer;

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
        Transform(await transform(this).ConfigureAwait(false));

    public static implicit operator T(ConsumeContext<T> context) => context.Payload;

    public Task DeleteMessage(CancellationToken ct = default) => Client.DeleteMessage(this, ct);

    public Task Acknowledge(CancellationToken ct = default) => DeleteMessage(ct);

    // public Task ChangeMessageVisibility(TimeSpan visibilityTimeout, CancellationToken ct = default) =>
    //     Client.ChangeMessageVisibility(this, visibilityTimeout, ct);
}
