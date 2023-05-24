using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LocalPost.KafkaConsumer.DependencyInjection;

public static class HealthChecks
{
    [SuppressMessage("ReSharper", "PossibleMultipleEnumeration")]
    public static IHealthChecksBuilder AddKafkaConsumerReadinessCheck<TKey, TValue>(this IHealthChecksBuilder builder,
        string name, HealthStatus? failureStatus = default, IEnumerable<string>? tags = default) => builder
        .Add(KafkaConsumerService<TKey, TValue>.QueueReadinessCheck(name, failureStatus, tags))
        .Add(KafkaConsumerService<TKey, TValue>.ConsumerGroupReadinessCheck(name, failureStatus, tags));

    [SuppressMessage("ReSharper", "PossibleMultipleEnumeration")]
    public static IHealthChecksBuilder AddKafkaConsumerLivenessCheck<TKey, TValue>(this IHealthChecksBuilder builder,
        string name, HealthStatus? failureStatus = default, IEnumerable<string>? tags = default) => builder
        .Add(KafkaConsumerService<TKey, TValue>.QueueLivenessCheck(name, failureStatus, tags))
        .Add(KafkaConsumerService<TKey, TValue>.ConsumerGroupLivenessCheck(name, failureStatus, tags));
}
