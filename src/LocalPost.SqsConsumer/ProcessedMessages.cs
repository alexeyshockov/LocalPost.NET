using System.Threading.Channels;
using Amazon.SQS;
using Amazon.SQS.Model;
using LocalPost;

namespace LocalPost.SqsConsumer;

internal sealed class ProcessedMessages : IBackgroundQueue<Message>, IAsyncEnumerable<DeleteMessageBatchRequest>
{
    private readonly string _queueUrl;
    private readonly Channel<DeleteMessageBatchRequestEntry> _messages =
        Channel.CreateUnbounded<DeleteMessageBatchRequestEntry>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });

    public ProcessedMessages(string queueUrl)
    {
        _queueUrl = queueUrl;
    }

    public ValueTask Enqueue(Message item, CancellationToken ct = default) => _messages.Writer.WriteAsync(
        new DeleteMessageBatchRequestEntry(Guid.NewGuid().ToString(), item.ReceiptHandle), ct);

    public IAsyncEnumerator<DeleteMessageBatchRequest> GetAsyncEnumerator(CancellationToken ct = default) =>
        _messages.Reader.ReadAllAsync(ct).Batch(() => new SqsDeleteBatchBuilder(_queueUrl)).GetAsyncEnumerator(ct);
}

internal sealed class ProcessedMessagesHandler : IMessageHandler<DeleteMessageBatchRequest>
{
    private readonly IAmazonSQS _sqs;

    public ProcessedMessagesHandler(IAmazonSQS sqs)
    {
        _sqs = sqs;
    }

    public async Task Process(DeleteMessageBatchRequest payload, CancellationToken ct)
    {
        var response = await _sqs.DeleteMessageBatchAsync(payload, ct);
        if (response.Failed.Any())
        {
            // TODO Log failures
        }
    }
}
