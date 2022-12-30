using System.Threading.Channels;
using Amazon.SimpleNotificationService.Model;

namespace LocalPost.SnsPublisher;

public interface ISnsPublisher
{
    IBackgroundQueue<PublishBatchRequestEntry> ForTopic(string arn);
}

internal sealed class Publisher : ISnsPublisher, IAsyncEnumerable<PublishBatchRequest>, IDisposable
{
    private sealed class TopicPublishingQueue : IBackgroundQueue<PublishBatchRequestEntry>
    {
        private readonly Channel<PublishBatchRequestEntry> _batchEntries;

        public TopicPublishingQueue(string arn)
        {
            _batchEntries = Channel.CreateUnbounded<PublishBatchRequestEntry>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });
            Results = _batchEntries.Reader.ReadAllAsync().Batch(() => new SnsBatchBuilder(arn));
        }

        public IAsyncEnumerable<PublishBatchRequest> Results { get; }

        public ValueTask Enqueue(PublishBatchRequestEntry item, CancellationToken ct = default)
        {
            if (item.CalculateSize() > PublisherOptions.RequestMaxSize)
                throw new ArgumentOutOfRangeException(nameof(item), "Message is too big");

            return _batchEntries.Writer.WriteAsync(item, ct);
        }
    }

    private readonly Dictionary<string, TopicPublishingQueue> _channels = new();
    private readonly AsyncEnumerableMerger<PublishBatchRequest> _combinedReader = new(true);

    private TopicPublishingQueue Create(string arn)
    {
        var q = _channels[arn] = new TopicPublishingQueue(arn);
        _combinedReader.Add(q.Results);

        return q;
    }

    public IBackgroundQueue<PublishBatchRequestEntry> ForTopic(string arn) =>
        _channels.TryGetValue(arn, out var queue) ? queue : Create(arn);

    public void Dispose()
    {
        _combinedReader.Dispose();
    }

    public IAsyncEnumerator<PublishBatchRequest> GetAsyncEnumerator(CancellationToken ct = default) =>
        _combinedReader.GetAsyncEnumerator(ct);
}
