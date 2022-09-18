using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Polly;

namespace LocalPost.AmazonSns;

internal sealed class Sender
{
    private readonly IAsyncPolicy _policy;
    private readonly IAmazonSimpleNotificationService _sns;

    public Sender(IAmazonSimpleNotificationService sns)
    {
        _policy = ResiliencePolicy.Create();
        _sns = sns;
    }

    public async Task Send(PublishBatchRequest payload, CancellationToken ct)
    {
        var batchResponse = await _policy.ExecuteAsync(tryToken => _sns.PublishBatchAsync(payload, tryToken), ct);

        if (batchResponse.Failed.Any())
        {
            // TODO Report individual errors...
        }
    }
}
