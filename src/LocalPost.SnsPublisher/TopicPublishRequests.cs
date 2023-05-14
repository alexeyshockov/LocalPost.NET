using Amazon.SimpleNotificationService.Model;

namespace LocalPost.SnsPublisher;

internal sealed partial class Publisher
{
    private sealed class TopicPublishRequests : IBackgroundQueueManager<PublishBatchRequestEntry>,
        IBackgroundQueue<PublishBatchRequestEntry>, IAsyncEnumerable<PublishBatchRequest>
    {
        private readonly string _arn;
        private readonly BackgroundQueue<PublishBatchRequestEntry> _queue;

        public TopicPublishRequests(QueueOptions options, string arn)
        {
            _arn = arn;
            _queue = new BackgroundQueue<PublishBatchRequestEntry>(options);
        }

        public IAsyncEnumerator<PublishBatchRequest> GetAsyncEnumerator(
            CancellationToken cancellationToken = default) =>
            _queue.Batch(() => new SnsBatchBuilder(_arn)).GetAsyncEnumerator(cancellationToken);

        public ValueTask Enqueue(PublishBatchRequestEntry item, CancellationToken ct = default)
        {
            if (item.CalculateSize() > PublisherOptions.RequestMaxSize)
                throw new ArgumentOutOfRangeException(nameof(item), "Message is too big");

            return _queue.Enqueue(item, ct);
        }

        public bool IsClosed => _queue.IsClosed;

        public ValueTask CompleteAsync(CancellationToken ct = default) => _queue.CompleteAsync(ct);
    }
}
