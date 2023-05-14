using System.Diagnostics.CodeAnalysis;
using Amazon.SimpleNotificationService.Model;
using LocalPost.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LocalPost.SnsPublisher.DependencyInjection;

public static class HealthChecks
{
    [SuppressMessage("ReSharper", "PossibleMultipleEnumeration")]
    public static IHealthChecksBuilder AddAmazonSnsBatchPublisherReadinessCheck(this IHealthChecksBuilder builder,
        string name, HealthStatus? failureStatus = default, IEnumerable<string>? tags = default,
        TimeSpan? timeout = default) => builder
        // FIXME Add queue supervisor
        .AddBackgroundQueueConsumerReadinessCheck<PublishBatchRequest>(name, failureStatus, tags, timeout);

    [SuppressMessage("ReSharper", "PossibleMultipleEnumeration")]
    public static IHealthChecksBuilder AddAmazonSnsBatchPublisherLivenessCheck(this IHealthChecksBuilder builder,
        string name, HealthStatus? failureStatus = default, IEnumerable<string>? tags = default,
        TimeSpan? timeout = default) => builder
        // FIXME Add queue supervisor
        .AddBackgroundQueueConsumerLivenessCheck<PublishBatchRequest>(name, failureStatus, tags, timeout);

    // TODO Optional checks for the in-memory queues... Like if they are full or not
}
