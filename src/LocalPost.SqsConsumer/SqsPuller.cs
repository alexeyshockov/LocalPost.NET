using System.Collections.Immutable;
using Amazon.SQS;
using Amazon.SQS.Model;
using LocalPost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LocalPost.SqsConsumer;

internal static partial class ServiceProviderExtensions
{
    public static SqsPuller GetSqs(this IServiceProvider provider, string name) =>
        provider.GetRequiredService<SqsAccessor>()[name];
}

internal sealed class SqsAccessor
{
    private readonly IReadOnlyDictionary<string, SqsPuller> _registry;

    public SqsAccessor(IEnumerable<SqsPuller> registry)
    {
        _registry = registry.ToImmutableDictionary(x => x.Name, x => x);
    }

    public SqsPuller this[string name] => _registry[name];
}

internal sealed class SqsPuller : IAsyncEnumerable<Message>
{
    private readonly IAmazonSQS _sqs;
    private readonly SqsConsumerOptions _options;

    private readonly IBackgroundQueue<Message> _processedMessages;

    public SqsPuller(IAmazonSQS sqs, string name, IOptionsMonitor<SqsConsumerOptions> options)
    {
        _sqs = sqs;
        _options = options.Get(name);

        var processedMessages = new ProcessedMessages(_options.QueueUrl);
        _processedMessages = processedMessages;
        ProcessedMessages = processedMessages;

        Name = name;
    }

    public string Name { get; }

    public IAsyncEnumerable<DeleteMessageBatchRequest> ProcessedMessages { get; }

    public async IAsyncEnumerator<Message> GetAsyncEnumerator(CancellationToken ct = default)
    {
        var attributeNames = SqsConsumerOptions.AllAttributes.ToList(); // TODO Configurable
        var messageAttributeNames = SqsConsumerOptions.AllMessageAttributes.ToList(); // TODO Configurable

        while (!ct.IsCancellationRequested)
        {
            // AWS SDK handles network failures, see
            // https://docs.aws.amazon.com/sdkref/latest/guide/feature-retry-behavior.html
            var receiveMessageResponse = await _sqs.ReceiveMessageAsync(new ReceiveMessageRequest
            {
                QueueUrl = _options.QueueUrl,
                WaitTimeSeconds = _options.WaitTimeSeconds,
                MaxNumberOfMessages = _options.MaxNumberOfMessages,
                AttributeNames = attributeNames,
                MessageAttributeNames = messageAttributeNames,
            }, ct);

            foreach (var message in receiveMessageResponse.Messages)
                yield return message;
        }
    }

    public MessageHandler<Message> Handler(MessageHandler<Message> handler) => async (payload, ct) =>
    {
        await handler(payload, ct);

        // Won't be deleted in case of an exception in the handler
        await _processedMessages.Enqueue(payload, ct);

        // Extend message's VisibilityTimeout in case of long processing?..
    };
}
