using System.ComponentModel.DataAnnotations;

namespace LocalPost.SqsConsumer;

/// <summary>
///     General SQS consumer settings
/// </summary>
public sealed record Options
{
    internal static readonly List<string> AllAttributes = new() { "All" };
    internal static readonly List<string> AllMessageAttributes = new() { "All" };

    public const int DefaultTimeout = 30;

    /// <summary>
    ///     How many messages to process in parallel. Default is 10.
    /// </summary>
    [Required] public ushort MaxConcurrency { get; set; } = 10;

    [Required] public string QueueName { get; set; } = null!;

    private string? _queueUrl;
    /// <summary>
    ///     If not set, IAmazonSQS.GetQueueUrlAsync(QueueName) will be used once, to get the actual URL of the queue.
    /// </summary>
    [Url] public string? QueueUrl
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
    [Range(0, 20)] public byte WaitTimeSeconds { get; set; } = 20;

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
    [Range(1, 10)] public byte MaxNumberOfMessages { get; set; } = 10;

    /// <summary>
    ///     Message processing timeout, in seconds. If not set, IAmazonSQS.GetQueueAttributesAsync() will be used once,
    ///     to get VisibilityTimeout for the queue.
    /// </summary>
    /// <see href="https://docs.aws.amazon.com/AWSSimpleQueueService/latest/SQSDeveloperGuide/sqs-visibility-timeout.html">
    ///     Amazon SQS visibility timeout
    /// </see>
    [Range(1, 43200)]
    public int Timeout { get; set; } = DefaultTimeout;
}
