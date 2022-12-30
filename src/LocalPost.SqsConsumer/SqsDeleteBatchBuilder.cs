using Amazon.SQS.Model;

namespace LocalPost.SqsConsumer;

internal sealed class SqsDeleteBatchBuilder : BatchBuilder<DeleteMessageBatchRequestEntry, DeleteMessageBatchRequest>
{
    private readonly DeleteMessageBatchRequest _batchRequest;

    public SqsDeleteBatchBuilder(string queueUrl) : base(TimeSpan.FromSeconds(1)) // TODO Configurable
    {
        _batchRequest = new DeleteMessageBatchRequest { QueueUrl = queueUrl };
    }

    public override bool IsEmpty => _batchRequest.Entries.Count == 0;

    private bool CanFit(DeleteMessageBatchRequestEntry entry) => _batchRequest.Entries.Count <= 10;

    public override bool TryAdd(DeleteMessageBatchRequestEntry entry)
    {
        var canFit = CanFit(entry);
        if (!canFit)
            return false;

        _batchRequest.Entries.Add(entry);

        return true;
    }

    public override DeleteMessageBatchRequest Build() => _batchRequest;
}
