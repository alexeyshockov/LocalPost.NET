using Amazon.SQS;
using Amazon.SQS.Model;
using LocalPost.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LocalPost.SqsConsumer;

internal sealed class QueueClient : INamedService
{
    private readonly ILogger<QueueClient> _logger;

    private readonly IAmazonSQS _sqs;
    private readonly Options _options;

    public QueueClient(ILogger<QueueClient> logger, IAmazonSQS sqs, IOptionsMonitor<Options> options, string name) :
        this(logger, sqs, options.Get(name), name)
    {
    }

    public QueueClient(ILogger<QueueClient> logger, IAmazonSQS sqs, Options options, string name)
    {
        _logger = logger;
        _sqs = sqs;
        _options = options;
        Name = name;
    }

    public string Name { get; }

    public string QueueName => _options.QueueName;

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
        if (string.IsNullOrEmpty(_options.QueueUrl))
            // Checking for a possible error in the response would be also good...
            _queueUrl = (await _sqs.GetQueueUrlAsync(_options.QueueName, ct)).QueueUrl;

        await FetchQueueAttributesAsync(ct);
    }

    private async Task FetchQueueAttributesAsync(CancellationToken ct)
    {
        try
        {
            // Checking for a possible error in the response would be also good...
            _queueAttributes = await _sqs.GetQueueAttributesAsync(QueueUrl, EndpointOptions.AllAttributes, ct);
        }
        catch (OperationCanceledException e) when (e.CancellationToken == ct)
        {
            throw;
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "Cannot fetch attributes for SQS {Queue}", _options.QueueName);
        }
    }

    public async Task<IEnumerable<Message>> PullMessagesAsync(CancellationToken ct)
    {
        using var activity = SqsActivitySource.StartReceiving(this);

        var attributeNames = EndpointOptions.AllAttributes; // Make configurable, later
        var messageAttributeNames = EndpointOptions.AllMessageAttributes; // Make configurable, later

        // AWS SDK handles network failures, see
        // https://docs.aws.amazon.com/sdkref/latest/guide/feature-retry-behavior.html
        var response = await _sqs.ReceiveMessageAsync(new ReceiveMessageRequest
        {
            QueueUrl = QueueUrl,
            WaitTimeSeconds = _options.WaitTimeSeconds,
            MaxNumberOfMessages = _options.MaxNumberOfMessages,
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
        using var activity = SqsActivitySource.StartSettling(context);
        await _sqs.DeleteMessageAsync(QueueUrl, context.ReceiptHandle);

        // TODO Log failures?..
    }

    public async Task DeleteMessagesAsync<T>(BatchConsumeContext<T> context)
    {
        using var activity = SqsActivitySource.StartSettling(context);

        var requests = context.Messages
            .Select((message, i) => new DeleteMessageBatchRequestEntry(i.ToString(), message.ReceiptHandle))
            .Chunk(10)
            .Select(entries => entries.ToList());

        await Task.WhenAll(requests.Select(entries =>
            _sqs.DeleteMessageBatchAsync(QueueUrl, entries)));

        // TODO Log failures?..
    }
}
