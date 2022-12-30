using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Microsoft.Extensions.Logging;

namespace LocalPost.SnsPublisher;

/// <summary>
/// Default implementation
/// </summary>
internal sealed class Sender
{
    private readonly ILogger<Sender> _logger;
    private readonly IAmazonSimpleNotificationService _sns;

    public Sender(ILogger<Sender> logger, IAmazonSimpleNotificationService sns)
    {
        _logger = logger;
        _sns = sns;
    }

    public async Task Send(PublishBatchRequest payload, CancellationToken ct)
    {
        _logger.LogTrace("Sending a batch of {Amount} publish request(s) to SNS...", payload.PublishBatchRequestEntries.Count);
        var batchResponse = await _sns.PublishBatchAsync(payload, ct);

        if (batchResponse.Failed.Any())
            _logger.LogError("Batch entries failed: {FailedAmount} from {Amount}",
                batchResponse.Failed.Count, payload.PublishBatchRequestEntries.Count);
    }
}
