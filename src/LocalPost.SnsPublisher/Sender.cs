using System.Diagnostics;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Microsoft.Extensions.Logging;

namespace LocalPost.SnsPublisher;

internal sealed class Sender : IHandler<PublishBatchRequest>
{
    private static readonly ActivitySource Tracer = new(typeof(Sender).Namespace);

    private readonly ILogger<Sender> _logger;
    private readonly IAmazonSimpleNotificationService _sns;

    public Sender(ILogger<Sender> logger, IAmazonSimpleNotificationService sns)
    {
        _logger = logger;
        _sns = sns;
    }

    public async Task InvokeAsync(PublishBatchRequest payload, CancellationToken ct)
    {
        using var span = Tracer.StartActivity();

        _logger.LogTrace("Sending a batch of {Amount} message(s) to SNS...", payload.PublishBatchRequestEntries.Count);
        var batchResponse = await _sns.PublishBatchAsync(payload, ct);

        if (batchResponse.Failed.Any())
            _logger.LogError("Batch entries failed: {FailedAmount} from {Amount}",
                batchResponse.Failed.Count, payload.PublishBatchRequestEntries.Count);
    }
}
