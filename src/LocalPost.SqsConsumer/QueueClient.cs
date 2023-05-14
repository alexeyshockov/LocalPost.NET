using System.Diagnostics;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LocalPost.SqsConsumer;

internal sealed class QueueClient
{
    // TODO Add more details
    // See https://github.com/npgsql/npgsql/blob/main/src/Npgsql/NpgsqlActivitySource.cs#LL61C31-L61C49
    // See https://github.com/npgsql/npgsql/blob/main/src/Npgsql/NpgsqlCommand.cs#L1639-L1644
    private static readonly ActivitySource Tracer = new(typeof(QueueClient).Namespace);

    private readonly ILogger<QueueClient> _logger;
    private readonly IAmazonSQS _sqs;
    private readonly Options _options;

    public QueueClient(ILogger<QueueClient> logger, string name, IOptionsMonitor<Options> options, IAmazonSQS sqs) :
        this(logger, options.Get(name), sqs)
    {
    }

    public QueueClient(ILogger<QueueClient> logger, Options options, IAmazonSQS sqs)
    {
        _logger = logger;
        _sqs = sqs;
        _options = options;
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
            _queueAttributes = await _sqs.GetQueueAttributesAsync(QueueUrl, Options.AllAttributes, ct);
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

        var attributeNames = Options.AllAttributes; // TODO Configurable
        var messageAttributeNames = Options.AllMessageAttributes; // TODO Configurable

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
        catch (Exception)
        {
            // Just bubble up, so the supervisor can report the error and the whole app can be restarted (Kubernetes)
            throw;
        }
    }
}
