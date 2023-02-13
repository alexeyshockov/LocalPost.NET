using System.Diagnostics;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LocalPost.SqsConsumer;

internal sealed class QueueClient
{
    private static readonly ActivitySource Tracer = new(typeof(QueueClient).Namespace);

    private readonly ILogger<QueueClient> _logger;
    private readonly IAmazonSQS _sqs;
    private readonly ConsumerOptions _options;

    public QueueClient(ILogger<QueueClient> logger, string name, IAmazonSQS sqs, IOptionsMonitor<ConsumerOptions> options)
    {
        _logger = logger;
        _sqs = sqs;
        _options = options.Get(name);
    }

    private GetQueueAttributesResponse? _queueAttributes;

    // TODO Use
    public int MessageVisibilityTimeout => _queueAttributes?.VisibilityTimeout switch
    {
        > 0 => _queueAttributes.VisibilityTimeout,
        _ => _options.Timeout
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
            _queueAttributes = await _sqs.GetQueueAttributesAsync(QueueUrl, ConsumerOptions.AllAttributes, ct);
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

    public async Task DeleteMessageAsync(Message message, CancellationToken ct)
    {
        using var span = Tracer.StartActivity();

        var response = await _sqs.DeleteMessageAsync(QueueUrl, message.ReceiptHandle, ct);

        // TODO Log failures?..
    }

    public async Task<IEnumerable<ConsumeContext>> PullMessagesAsync(CancellationToken ct)
    {
        using var span = Tracer.StartActivity();

        var attributeNames = ConsumerOptions.AllAttributes; // TODO Configurable
        var messageAttributeNames = ConsumerOptions.AllMessageAttributes; // TODO Configurable

        try
        {
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

            // TODO Add number of received messages to the diagnostics span
            return response.Messages
                .Select(message => new ConsumeContext(_options.QueueName, QueueUrl, message)).ToArray();
        }
//        catch (OverLimitException)
//        {
//            // TODO Handle
//        }
        catch (OperationCanceledException e) when (e.CancellationToken == ct)
        {
            throw;
        }
        catch (Exception e)
        {
            // FIXME Error handler
        }

        return Array.Empty<ConsumeContext>();
    }
}
