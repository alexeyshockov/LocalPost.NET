using System.Text;
using Amazon.SimpleNotificationService.Model;

namespace LocalPost.AmazonSns;

internal sealed class SnsBatch : IBatchBuilder<PublishBatchRequestEntry, PublishBatchRequest>, IDisposable
{
    private readonly CancellationTokenSource _timeWindow = new(TimeSpan.FromSeconds(1)); // TODO Configurable
    private PublishBatchRequest? _batchRequest;

    public SnsBatch(string topicArn)
    {
        _batchRequest = new PublishBatchRequest
        {
            TopicArn = topicArn
        };
    }

    private PublishBatchRequest BatchRequest => _batchRequest ?? throw new ObjectDisposedException(nameof(SnsBatch));

    public CancellationToken TimeWindow => _timeWindow.Token;
    public bool IsEmpty => BatchRequest.PublishBatchRequestEntries.Count == 0;

    // 10 entries max, 256KB max
    public bool CanFit(PublishBatchRequestEntry entry) =>
        10 > BatchRequest.PublishBatchRequestEntries.Count
        &&
        262_144 > BatchRequest.PublishBatchRequestEntries.Append(entry)
            .Aggregate(0, (total, e) => total + e.CalculateSize());

    public bool Add(PublishBatchRequestEntry entry)
    {
        var canFit = CanFit(entry);

        if (canFit)
            BatchRequest.PublishBatchRequestEntries.Add(entry);

        return canFit;
    }

    public PublishBatchRequest BuildAndDispose()
    {
        var result = BatchRequest;
        Dispose();

        return result;
    }

    public void Dispose()
    {
        _timeWindow.Dispose();
        _batchRequest = null;
    }
}

internal static class PublishBatchRequestEntryExtensions
{
    public static int CalculateSize(this PublishBatchRequestEntry entry) => Encoding.UTF8.GetByteCount(entry.Message);
}
