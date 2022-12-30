using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using Amazon.SQS;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LocalPost.SqsConsumer;

/// <summary>
///     General SQS consumer settings
/// </summary>
public sealed record SqsConsumerOptions
{
    public static readonly ImmutableArray<string> AllAttributes = ImmutableArray.Create("All");
    public static readonly ImmutableArray<string> AllMessageAttributes = ImmutableArray.Create("All");

    public const ushort DefaultTimeout = 30;

    /// <summary>
    ///     How many messages to process in parallel.
    /// </summary>
    [Required] public ushort MaxConcurrency { get; set; } = 10;

    [Required] public string QueueName { get; set; } = null!;

    /// <summary>
    ///     If not set, IAmazonSQS.GetQueueUrlAsync(QueueName) will be used once, to get the actual URL of the queue.
    /// </summary>
    [Url] public string? QueueUrl { get; set; }

    /// <summary>
    ///     Time to wait for available messages in the queue.
    /// </summary>
    /// <see href="https://docs.aws.amazon.com/AWSSimpleQueueService/latest/SQSDeveloperGuide/sqs-short-and-long-polling.html">
    ///     Amazon SQS short and long polling
    /// </see>
    [Range(0, 60)] public byte WaitTimeSeconds { get; set; } = 20;

    [Range(1, 10)] public byte MaxNumberOfMessages { get; set; } = 10;

    /// <summary>
    ///     Message processing timeout, in seconds. If not set, IAmazonSQS.GetQueueAttributesAsync() will be used once, to get
    ///     VisibilityTimeout for the queue.
    /// </summary>
    /// <see href="https://docs.aws.amazon.com/AWSSimpleQueueService/latest/SQSDeveloperGuide/sqs-visibility-timeout.html">
    ///     Amazon SQS visibility timeout
    /// </see>
    [Range(1, 43200)]
    public ushort Timeout { get; set; }
}

internal sealed class SqsConsumerOptionsResolver : IPostConfigureOptions<SqsConsumerOptions>
{
    private readonly ILogger<SqsConsumerOptionsResolver> _logger;
    private readonly IAmazonSQS _sqs;

    public SqsConsumerOptionsResolver(ILogger<SqsConsumerOptionsResolver> logger, IAmazonSQS sqs)
    {
        _logger = logger;
        _sqs = sqs;
    }

    public void PostConfigure(string name, SqsConsumerOptions options)
    {
        if (string.IsNullOrEmpty(options.QueueUrl))
            FetchQueueUrl(options).Wait();

        if (options.Timeout == 0) // Try to get it from the SQS settings if not set
            FetchVisibilityTimeout(options).Wait();
    }

    private async Task FetchQueueUrl(SqsConsumerOptions options)
    {
        try
        {
            // Checking possible errors in the response would be good
            options.QueueUrl = (await _sqs.GetQueueUrlAsync(options.QueueName)).QueueUrl;
        }
        catch (Exception e)
        {
            // TODO Wrap in our own exception
            throw new ArgumentException($"Cannot fetch SQS URL for {options.QueueName}", nameof(options), e);
        }
    }

    private async Task FetchVisibilityTimeout(SqsConsumerOptions options)
    {
        try
        {
            // Checking possible errors in the response would be good
            var queueAttributes = await _sqs
                .GetQueueAttributesAsync(options.QueueUrl, SqsConsumerOptions.AllAttributes.ToList());

            options.Timeout = queueAttributes.VisibilityTimeout switch
            {
                > 0 => (ushort) queueAttributes.VisibilityTimeout,
                _ => SqsConsumerOptions.DefaultTimeout
            };
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "Cannot fetch SQS attributes for {Queue}", options.QueueName);
            options.Timeout = SqsConsumerOptions.DefaultTimeout;
        }
    }
}
