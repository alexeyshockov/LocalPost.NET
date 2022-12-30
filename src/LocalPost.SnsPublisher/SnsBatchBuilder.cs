using Amazon.SimpleNotificationService.Model;
using Nito.AsyncEx;

namespace LocalPost.SnsPublisher;

internal sealed class SnsBatchBuilder : IBatchBuilder<PublishBatchRequestEntry, PublishBatchRequest>
{
    private readonly CancellationTokenSource _timeWindow = new(TimeSpan.FromSeconds(1)); // TODO Configurable
    private readonly CancellationTokenTaskSource<bool> _timeWindowTrigger;
    private PublishBatchRequest? _batchRequest;

    public SnsBatchBuilder(string topicArn)
    {
        _batchRequest = new PublishBatchRequest
        {
            TopicArn = topicArn
        };

        _timeWindowTrigger = new CancellationTokenTaskSource<bool>(_timeWindow.Token);
    }

    private PublishBatchRequest BatchRequest => _batchRequest ?? throw new ObjectDisposedException(nameof(SnsBatchBuilder));

    public CancellationToken TimeWindow => _timeWindow.Token;
    public Task<bool> TimeWindowTrigger => _timeWindowTrigger.Task;
    public bool IsEmpty => BatchRequest.PublishBatchRequestEntries.Count == 0;

    private bool CanFit(PublishBatchRequestEntry entry) =>
        PublisherOptions.BatchMaxSize > BatchRequest.PublishBatchRequestEntries.Count
        &&
        PublisherOptions.RequestMaxSize > BatchRequest.PublishBatchRequestEntries.Append(entry)
            .Aggregate(0, (total, e) => total + e.CalculateSize());

    public bool TryAdd(PublishBatchRequestEntry entry)
    {
        var canFit = CanFit(entry);
        if (!canFit)
            return false;

        if (string.IsNullOrEmpty(entry.Id))
            entry.Id = Guid.NewGuid().ToString();

        BatchRequest.PublishBatchRequestEntries.Add(entry);

        return true;
    }

    public PublishBatchRequest Build() => BatchRequest;

    public void Dispose()
    {
        _timeWindow.Dispose();
        _timeWindowTrigger.Dispose();
        _batchRequest = null; // Just make it unusable
    }
}
