using System.Threading.Channels;
using Amazon.SimpleNotificationService.Model;

namespace LocalPost.AmazonSns;

public interface ISnsPublisher
{
    IBackgroundQueue<PublishBatchRequestEntry> ForTopic(string arn);
}

internal sealed class Publisher : ISnsPublisher, IBackgroundQueueReader<PublishBatchRequest>, IDisposable
{
    private sealed class TopicPublishingQueue : IBackgroundQueue<PublishBatchRequestEntry>
    {
        public Channel<PublishBatchRequestEntry> BatchEntries { get; }
        public ChannelReader<PublishBatchRequest> Batches { get; }

        public TopicPublishingQueue(string arn)
        {
            BatchEntries = Channel.CreateUnbounded<PublishBatchRequestEntry>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });
            Batches = BatchEntries.Reader.Batch(() => new SnsBatch(arn));
        }

        public ValueTask Enqueue(PublishBatchRequestEntry item, CancellationToken ct) =>
            BatchEntries.Writer.WriteAsync(item, ct);
    }

    private readonly Dictionary<string, TopicPublishingQueue> _channels = new();
    private readonly CombinedChannelReader<PublishBatchRequest> _combinedReader = new();

    private TopicPublishingQueue Create(string arn)
    {
        var q = _channels[arn] = new TopicPublishingQueue(arn);
        _combinedReader.Add(q.Batches);

        return q;
    }

    public IBackgroundQueue<PublishBatchRequestEntry> ForTopic(string arn) =>
        _channels.TryGetValue(arn, out var queue) ? queue : Create(arn);

    public ChannelReader<PublishBatchRequest> Reader => _combinedReader;

    public void Dispose()
    {
        _combinedReader.Dispose();
    }
}
