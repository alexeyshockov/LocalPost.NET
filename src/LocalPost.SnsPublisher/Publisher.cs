using System.Collections.Immutable;
using Amazon.SimpleNotificationService.Model;

namespace LocalPost.SnsPublisher;

public interface ISnsPublisher
{
    IBackgroundQueue<PublishBatchRequestEntry> ForTopic(string arn);
}

internal sealed partial class Publisher : ISnsPublisher, IAsyncEnumerable<PublishBatchRequest>,
    IBackgroundQueueManager<PublishBatchRequest>, IDisposable
{
    private ImmutableDictionary<string, TopicPublishRequests> _channels =
        ImmutableDictionary<string, TopicPublishRequests>.Empty;

    private readonly AsyncEnumerableMerger<PublishBatchRequest> _combinedReader = new(true);

    private readonly PublisherOptions _options;

    public Publisher(PublisherOptions options)
    {
        _options = options;
    }

    public bool IsClosed { get; private set; }

    public IBackgroundQueue<PublishBatchRequestEntry> ForTopic(string arn) =>
        ImmutableInterlocked.GetOrAdd(ref _channels, arn, RegisterQueueFor);

    private TopicPublishRequests RegisterQueueFor(string arn)
    {
        var queue = new TopicPublishRequests(_options.PerTopic, arn);
        _combinedReader.Add(queue);

        return queue;
    }

    public IAsyncEnumerator<PublishBatchRequest> GetAsyncEnumerator(CancellationToken ct = default) =>
        _combinedReader.GetAsyncEnumerator(ct);

    public async ValueTask CompleteAsync(CancellationToken ct = default)
    {
        // TODO Do not allow to register new topics, as they won't be processed here...
        await Task.WhenAll(_channels.Values.Select(q => q.CompleteAsync(ct).AsTask()));
        IsClosed = true;
    }

    public void Dispose()
    {
        _combinedReader.Dispose();
        _channels = ImmutableDictionary<string, TopicPublishRequests>.Empty;
    }
}
