using Amazon.Runtime;
using Amazon.SQS;
using Amazon.SQS.Model;
using Polly;
using Polly.Retry;

namespace LocalPost.SqsConsumer;

internal sealed class QueueClient(ILogger logger, IAmazonSQS sqs, ConsumerOptions options)
{
    public string QueueName => options.QueueName;

    private GetQueueAttributesResponse? _queueAttributes;

    private readonly ResiliencePipeline _pipeline = new ResiliencePipelineBuilder()
        .AddRetry(new RetryStrategyOptions
        {
            ShouldHandle = new PredicateBuilder()
                .Handle<AmazonServiceException>(e => e.Retryable is not null),

            Delay = TimeSpan.FromSeconds(1),
            MaxRetryAttempts = byte.MaxValue,
            BackoffType = DelayBackoffType.Exponential,

            // Initially, aim for an exponential backoff, but after a certain number of retries, set a maximum delay
            MaxDelay = TimeSpan.FromMinutes(1),
            UseJitter = true
        })
        .Build();

    // TODO Use
    public TimeSpan? MessageVisibilityTimeout => _queueAttributes?.VisibilityTimeout switch
    {
        > 0 => TimeSpan.FromSeconds(_queueAttributes.VisibilityTimeout),
        _ => null
    };

    private string? _queueUrl;
    private string QueueUrl => _queueUrl ?? throw new InvalidOperationException("SQS queue client is not connected");

    public async Task Connect(CancellationToken ct)
    {
        if (string.IsNullOrEmpty(options.QueueUrl))
            // Checking for a possible error in the response would be also good...
            _queueUrl = (await sqs.GetQueueUrlAsync(options.QueueName, ct).ConfigureAwait(false)).QueueUrl;

        await FetchQueueAttributes(ct).ConfigureAwait(false);
    }

    private async Task FetchQueueAttributes(CancellationToken ct)
    {
        try
        {
            // Checking for a possible error in the response would be also good...
            _queueAttributes = await sqs.GetQueueAttributesAsync(QueueUrl, ["All"], ct).ConfigureAwait(false);
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

    public async Task<IEnumerable<Message>> PullMessages(CancellationToken ct) =>
        await _pipeline.ExecuteAsync(PullMessagesCore, ct).ConfigureAwait(false);

    private async ValueTask<IEnumerable<Message>> PullMessagesCore(CancellationToken ct)
    {
        using var activity = Tracing.StartReceiving(this);

        try
        {
            // AWS SDK handles network failures, see
            // https://docs.aws.amazon.com/sdkref/latest/guide/feature-retry-behavior.html
            var response = await sqs.ReceiveMessageAsync(new ReceiveMessageRequest
            {
                QueueUrl = QueueUrl,
                WaitTimeSeconds = options.WaitTimeSeconds,
                MaxNumberOfMessages = options.MaxNumberOfMessages,
                AttributeNames = options.AttributeNames,
                MessageAttributeNames = options.MessageAttributeNames,
            }, ct).ConfigureAwait(false);

            activity?.SetTagsFor(response);
            activity?.Success();

            return response.Messages;
        }
        catch (OperationCanceledException e) when (e.CancellationToken == ct)
        {
            throw;
        }
        catch (Exception e)
        {
            activity?.Error(e);
            throw;
        }
    }

    public async Task DeleteMessage<T>(ConsumeContext<T> context, CancellationToken ct = default)
    {
        using var activity = Tracing.StartSettling(context);
        await sqs.DeleteMessageAsync(QueueUrl, context.ReceiptHandle, ct).ConfigureAwait(false);

        // TODO Log failures?..
    }
}
