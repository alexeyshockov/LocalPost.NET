using JetBrains.Annotations;
using LocalPost.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LocalPost.KafkaConsumer.DependencyInjection;

[PublicAPI]
public static class HealthChecksBuilderEx
{
    // TODO AddKafkaConsumersLivenessCheck() — simply for all the registered consumers

    // Check if the same check is added twice?..

    public static IHealthChecksBuilder AddKafkaConsumerLivenessCheck(this IHealthChecksBuilder builder,
        string name, HealthStatus? failureStatus = default, IEnumerable<string>? tags = default) => builder
        .Add(HealthChecks.LivenessCheckForNamed<MessageSource>(name, failureStatus, tags))
        .AddNamedConsumerLivenessCheck<MessageSource, ConsumeContext<byte[]>>(name);

    public static IHealthChecksBuilder AddKafkaBatchConsumerLivenessCheck(this IHealthChecksBuilder builder,
        string name, HealthStatus? failureStatus = default, IEnumerable<string>? tags = default) => builder
        .Add(HealthChecks.LivenessCheckForNamed<BatchMessageSource>(name, failureStatus, tags))
        .AddNamedConsumerLivenessCheck<BatchMessageSource, BatchConsumeContext<byte[]>>(name);
}
