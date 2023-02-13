using System.Diagnostics.CodeAnalysis;
using LocalPost.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LocalPost.Azure.QueueConsumer.DependencyInjection;

public static class HealthChecks
{
    [SuppressMessage("ReSharper", "PossibleMultipleEnumeration")]
    public static IHealthChecksBuilder AddAmazonSqsConsumerReadinessCheck(this IHealthChecksBuilder builder,
        string name, HealthStatus? failureStatus = default, IEnumerable<string>? tags = default,
        TimeSpan? timeout = default) => builder
        .AddBackgroundServiceReadinessCheck<Consumer.Service>(name, failureStatus, tags, timeout)
        .AddBackgroundQueueConsumerReadinessCheck<ConsumeContext>(name, failureStatus, tags, timeout);

    [SuppressMessage("ReSharper", "PossibleMultipleEnumeration")]
    public static IHealthChecksBuilder AddAmazonSqsConsumerLivenessCheck(this IHealthChecksBuilder builder,
        string name, HealthStatus? failureStatus = default, IEnumerable<string>? tags = default,
        TimeSpan? timeout = default) => builder
        .AddBackgroundServiceReadinessCheck<Consumer.Service>(name, failureStatus, tags, timeout)
        .AddBackgroundQueueConsumerReadinessCheck<ConsumeContext>(name, failureStatus, tags, timeout);
}
