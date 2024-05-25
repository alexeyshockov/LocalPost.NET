using System.ComponentModel.DataAnnotations;
using JetBrains.Annotations;

namespace LocalPost.SqsConsumer;

/// <summary>
///     General SQS settings.
/// </summary>
[PublicAPI]
public record EndpointOptions
{
    // AWS SDK requires List<string>... No way to make it readonly / immutable :(
    internal static readonly List<string> AllAttributes = ["All"];
    internal static readonly List<string> AllMessageAttributes = ["All"];

    /// <summary>
    ///     How many messages to process concurrently. Default is 10.
    /// </summary>
    public ushort MaxConcurrency { get; set; } = 10;

    /// <summary>
    ///     Stop the consumer in case of an exception in the handler, or just log it and continue the processing loop.
    ///     Default is true.
    /// </summary>
    public bool BreakOnException { get; set; } = true;

    /// <summary>
    ///     How many messages to prefetch from SQS. Default is 10.
    /// </summary>
    public byte Prefetch { get; set; } = 10; // FIXME Use

    /// <summary>
    ///     Time to wait for available messages in the queue. 0 is short pooling, where 1..20 activates long pooling.
    ///     Default is 20.
    /// </summary>
    /// <see href="https://docs.aws.amazon.com/AWSSimpleQueueService/latest/SQSDeveloperGuide/sqs-short-and-long-polling.html">
    ///     Amazon SQS short and long polling
    /// </see>
    /// <see href="https://docs.aws.amazon.com/AWSSimpleQueueService/latest/SQSDeveloperGuide/working-with-messages.html#setting-up-long-polling">
    ///     Setting up long polling
    /// </see>
    [Range(0, 20)]
    public byte WaitTimeSeconds { get; set; } = 20;

    /// <summary>
    ///     The maximum number of messages to return. Amazon SQS never returns more messages than this value (however,
    ///     fewer messages might be returned). Valid values: 1 to 10. Default is 1.
    /// </summary>
    /// <see href="https://docs.aws.amazon.com/AWSSimpleQueueService/latest/SQSDeveloperGuide/sqs-short-and-long-polling.html">
    ///     Amazon SQS short and long polling
    /// </see>
    /// <see href="https://docs.aws.amazon.com/AWSSimpleQueueService/latest/SQSDeveloperGuide/working-with-messages.html#setting-up-long-polling">
    ///     Setting up long polling
    /// </see>
    [Range(1, 10)]
    public byte MaxNumberOfMessages { get; set; } = 10;

    // User specific thing...
//    /// <summary>
//    ///     Message processing timeout. If not set, IAmazonSQS.GetQueueAttributesAsync() will be used once,
//    ///     to get VisibilityTimeout for the queue. If it is not available, default value of 10 seconds will be used.
//    /// </summary>
//    /// <see href="https://docs.aws.amazon.com/AWSSimpleQueueService/latest/SQSDeveloperGuide/sqs-visibility-timeout.html">
//    ///     Amazon SQS visibility timeout
//    /// </see>
//    [Range(1, int.MaxValue)]
//    public int? TimeoutMilliseconds { get; set; }

    internal void UpdateFrom(EndpointOptions other)
    {
        MaxConcurrency = other.MaxConcurrency;
        Prefetch = other.Prefetch;
        WaitTimeSeconds = other.WaitTimeSeconds;
        MaxNumberOfMessages = other.MaxNumberOfMessages;
    }
}

/// <summary>
///     SQS queue consumer settings.
/// </summary>
[PublicAPI]
public record Options : EndpointOptions
{
    [Required]
    public string QueueName { get; set; } = null!;

    private string? _queueUrl;
    /// <summary>
    ///     If not set, IAmazonSQS.GetQueueUrlAsync(QueueName) will be used once, to get the actual URL of the queue.
    /// </summary>
    [Url]
    public string? QueueUrl
    {
        get => _queueUrl;
        set
        {
            _queueUrl = value;

            // Extract name (MyQueue) from an URL (https://sqs.us-east-2.amazonaws.com/123456789012/MyQueue)
            if (Uri.TryCreate(value, UriKind.Absolute, out var url) && url.Segments.Length >= 3)
                QueueName = url.Segments[2];
        }
    }
}

/// <summary>
///     SQS queue batch consumer settings.
/// </summary>
[PublicAPI]
public record BatchedOptions : Options
{
    [Range(1, ushort.MaxValue)]
    public ushort BatchMaxSize { get; set; } = 10;

    [Range(1, ushort.MaxValue)]
    public int BatchTimeWindowMilliseconds { get; set; } = 1_000;
}
