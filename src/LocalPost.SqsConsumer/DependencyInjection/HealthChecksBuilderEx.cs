using LocalPost.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LocalPost.SqsConsumer.DependencyInjection;

public static class HealthChecksBuilderEx
{
    // TODO AddSqsConsumersLivenessCheck() — simply for all the registered consumers

    // Check if the same check is added twice?..

    public static IHealthChecksBuilder AddSqsConsumerLivenessCheck(this IHealthChecksBuilder builder,
        string name, HealthStatus? failureStatus = default, IEnumerable<string>? tags = default) => builder
        .Add(HealthChecks.LivenessCheck<MessageSource>(name, failureStatus, tags))
        .AddPipelineLivenessCheck<MessageSource>(name);

    // public static IHealthChecksBuilder AddSqsBatchConsumerLivenessCheck(this IHealthChecksBuilder builder,
    //     string name, HealthStatus? failureStatus = default, IEnumerable<string>? tags = default) => builder
    //     .Add(HealthChecks.LivenessCheckForNamed<BatchMessageSource>(name, failureStatus, tags))
    //     .AddNamedConsumerLivenessCheck<BatchMessageSource, BatchConsumeContext<string>>(name);
}
