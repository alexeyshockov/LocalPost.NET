using System.Diagnostics.CodeAnalysis;
using LocalPost.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LocalPost.KafkaConsumer.DependencyInjection;

public static class HealthChecks
{
    [SuppressMessage("ReSharper", "PossibleMultipleEnumeration")]
    public static IHealthChecksBuilder AddKafkaConsumerReadinessCheck<TKey, TValue>(this IHealthChecksBuilder builder,
        string name, HealthStatus? failureStatus = default, IEnumerable<string>? tags = default,
        TimeSpan? timeout = default) => builder
        .AddBackgroundServiceReadinessCheck<MessageSource<TKey, TValue>.Service>(name, failureStatus, tags, timeout)
        .AddBackgroundQueueConsumerReadinessCheck<ConsumeContext<TKey, TValue>>(name, failureStatus, tags, timeout);

    [SuppressMessage("ReSharper", "PossibleMultipleEnumeration")]
    public static IHealthChecksBuilder AddKafkaConsumerLivenessCheck<TKey, TValue>(this IHealthChecksBuilder builder,
        string name, HealthStatus? failureStatus = default, IEnumerable<string>? tags = default,
        TimeSpan? timeout = default) => builder
        .AddBackgroundServiceLivenessCheck<MessageSource<TKey, TValue>.Service>(name, failureStatus, tags, timeout)
        .AddBackgroundQueueConsumerLivenessCheck<ConsumeContext<TKey, TValue>>(name, failureStatus, tags, timeout);
}
