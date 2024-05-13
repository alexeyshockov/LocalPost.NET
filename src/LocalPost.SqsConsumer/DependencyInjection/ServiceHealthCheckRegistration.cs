using LocalPost.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LocalPost.SqsConsumer.DependencyInjection;

public static class ServiceHealthCheckRegistration
{
    // Check if the same check is added twice?..
    public static IHealthChecksBuilder AddKafkaConsumerLivenessCheck(this IHealthChecksBuilder builder,
        string name, HealthStatus? failureStatus = default, IEnumerable<string>? tags = default) => builder
        .Add(HealthChecks.LivenessCheckForNamed<MessageSource>(name, failureStatus, tags))
        .AddConsumerGroupLivenessCheck<MessageSource, ConsumeContext<string>>();

    public static IHealthChecksBuilder AddKafkaBatchConsumerLivenessCheck(this IHealthChecksBuilder builder,
        string name, HealthStatus? failureStatus = default, IEnumerable<string>? tags = default) => builder
        .Add(HealthChecks.LivenessCheckForNamed<BatchMessageSource>(name, failureStatus, tags))
        .AddConsumerGroupLivenessCheck<BatchMessageSource, BatchConsumeContext<string>>();
}
