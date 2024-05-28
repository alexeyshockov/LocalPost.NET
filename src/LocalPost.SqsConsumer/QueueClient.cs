using Amazon.SQS;
using Amazon.SQS.Model;
using LocalPost.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LocalPost.SqsConsumer;

internal sealed class QueueClient(ILogger<QueueClient> logger, IAmazonSQS sqs, Options options, string name)
    : INamedService
{
    public QueueClient(ILogger<QueueClient> logger, IAmazonSQS sqs, IOptionsMonitor<Options> options, string name) :
        this(logger, sqs, options.Get(name), name)
    {
    }

    public string Name { get; } = name;

    public string QueueName => options.QueueName;

    private GetQueueAttributesResponse? _queueAttributes;

    // TODO Use
    public TimeSpan? MessageVisibilityTimeout => _queueAttributes?.VisibilityTimeout switch
    {
        > 0 => TimeSpan.FromSeconds(_queueAttributes.VisibilityTimeout),
        _ => null
    };

    private string? _queueUrl;
    private string QueueUrl => _queueUrl ?? throw new InvalidOperationException("SQS queue client is not connected");

    public async Task ConnectAsync(CancellationToken ct)
    {
        if (string.IsNullOrEmpty(options.QueueUrl))
            // Checking for a possible error in the response would be also good...
            _queueUrl = (await sqs.GetQueueUrlAsync(options.QueueName, ct)).QueueUrl;

        await FetchQueueAttributesAsync(ct);
    }

    private async Task FetchQueueAttributesAsync(CancellationToken ct)
    {
        try
        {
            // Checking for a possible error in the response would be also good...
            _queueAttributes = await sqs.GetQueueAttributesAsync(QueueUrl, EndpointOptions.AllAttributes, ct);
        }
        catch (OperationCanceledException e) when (e.CancellationToken == ct)
        {
            throw;
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Cannot fetch attributes for SQS {Queue}", options.QueueName);
        }
    }

    public async Task<IEnumerable<Message>> PullMessagesAsync(CancellationToken ct)
    {
        using var activity = Tracing.StartReceiving(this);

        var attributeNames = EndpointOptions.AllAttributes; // Make configurable, later
        var messageAttributeNames = EndpointOptions.AllMessageAttributes; // Make configurable, later

        // AWS SDK handles network failures, see
        // https://docs.aws.amazon.com/sdkref/latest/guide/feature-retry-behavior.html
        var response = await sqs.ReceiveMessageAsync(new ReceiveMessageRequest
        {
            QueueUrl = QueueUrl,
            WaitTimeSeconds = options.WaitTimeSeconds,
            MaxNumberOfMessages = options.MaxNumberOfMessages,
            AttributeNames = attributeNames,
            MessageAttributeNames = messageAttributeNames,
        }, ct);

        activity?.SetTagsFor(response);

        return response.Messages;

        // TODO Log failures?..

//        catch (OverLimitException)
//        {
//            // Log and try again?..
//        }
    }

    public async Task DeleteMessageAsync<T>(ConsumeContext<T> context)
    {
        using var activity = Tracing.StartSettling(context);
        await sqs.DeleteMessageAsync(QueueUrl, context.ReceiptHandle);

        // TODO Log failures?..
    }

    public async Task DeleteMessagesAsync<T>(BatchConsumeContext<T> context)
    {
        using var activity = Tracing.StartSettling(context);

        var requests = context.Messages
            .Select((message, i) => new DeleteMessageBatchRequestEntry(i.ToString(), message.ReceiptHandle))
            .Chunk(10)
            .Select(entries => entries.ToList());

        await Task.WhenAll(requests.Select(entries =>
            sqs.DeleteMessageBatchAsync(QueueUrl, entries)));

        // TODO Log failures?..
    }
}
