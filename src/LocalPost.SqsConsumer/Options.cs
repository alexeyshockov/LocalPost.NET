using System.ComponentModel.DataAnnotations;
using JetBrains.Annotations;

namespace LocalPost.SqsConsumer;

/// <summary>
///     General SQS settings.
/// </summary>
[PublicAPI]
public sealed class EndpointOptions
{
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

    public List<string> AttributeNames { get; set; } = ["All"];

    public List<string> MessageAttributeNames { get; set; } = ["All"];
}

/// <summary>
///     SQS queue consumer settings.
/// </summary>
[PublicAPI]
public sealed class ConsumerOptions
{
    [Range(1, ushort.MaxValue)]
    // public ushort MaxConcurrency { get; set; } = 10;
    public ushort Consumers { get; set; } = 1;

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
    ///     The maximum number of messages to return. Valid values: 1 to 10. Default is 10.
    ///
    ///     Amazon SQS never returns more messages than this value (however, fewer messages might be returned).
    ///
    ///     All the returned messages will be processed concurrently.
    /// </summary>
    /// <see href="https://docs.aws.amazon.com/AWSSimpleQueueService/latest/SQSDeveloperGuide/sqs-short-and-long-polling.html">
    ///     Amazon SQS short and long polling
    /// </see>
    /// <see href="https://docs.aws.amazon.com/AWSSimpleQueueService/latest/SQSDeveloperGuide/working-with-messages.html#setting-up-long-polling">
    ///     Setting up long polling
    /// </see>
    [Range(1, 10)]
    public byte MaxNumberOfMessages { get; set; } = 10;

    public List<string> AttributeNames { get; set; } = ["All"];

    public List<string> MessageAttributeNames { get; set; } = ["All"];

    internal void UpdateFrom(EndpointOptions global)
    {
        // MaxConcurrency = other.MaxConcurrency;
        // Prefetch = other.Prefetch;
        WaitTimeSeconds = global.WaitTimeSeconds;
        MaxNumberOfMessages = global.MaxNumberOfMessages;
        AttributeNames = global.AttributeNames;
        MessageAttributeNames = global.MessageAttributeNames;
    }

    internal void UpdateFrom(ConsumerOptions other)
    {
        WaitTimeSeconds = other.WaitTimeSeconds;
        MaxNumberOfMessages = other.MaxNumberOfMessages;
        AttributeNames = other.AttributeNames;
        MessageAttributeNames = other.MessageAttributeNames;
        QueueName = other.QueueName;
        _queueUrl = other._queueUrl;
    }

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
