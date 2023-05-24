using System.Diagnostics.CodeAnalysis;
using LocalPost.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LocalPost.SqsConsumer.DependencyInjection;

public static class HealthChecks
{
    [SuppressMessage("ReSharper", "PossibleMultipleEnumeration")]
    public static IHealthChecksBuilder AddAmazonSqsConsumerReadinessCheck(this IHealthChecksBuilder builder,
        string name, HealthStatus? failureStatus = default, IEnumerable<string>? tags = default) => builder
        .Add(SqsConsumerService.QueueReadinessCheck(name, failureStatus, tags))
        .Add(SqsConsumerService.ConsumerGroupReadinessCheck(name, failureStatus, tags));

    [SuppressMessage("ReSharper", "PossibleMultipleEnumeration")]
    public static IHealthChecksBuilder AddAmazonSqsConsumerLivenessCheck(this IHealthChecksBuilder builder,
        string name, HealthStatus? failureStatus = default, IEnumerable<string>? tags = default) => builder
        .Add(SqsConsumerService.QueueLivenessCheck(name, failureStatus, tags))
        .Add(SqsConsumerService.ConsumerGroupLivenessCheck(name, failureStatus, tags));
}
