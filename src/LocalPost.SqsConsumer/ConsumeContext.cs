using Amazon.SQS.Model;

namespace LocalPost.SqsConsumer;

public readonly record struct ConsumeContext(string QueueName, string QueueUrl, Message Message)
{
    public readonly DateTimeOffset ReceivedAt = DateTimeOffset.Now;

    public bool IsStale => false; // TODO Check the visibility timeout
}
