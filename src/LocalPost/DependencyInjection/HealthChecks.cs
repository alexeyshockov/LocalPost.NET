using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LocalPost.DependencyInjection;

public static class HealthChecks
{
    public static IHealthChecksBuilder AddBackgroundQueueReadinessCheck<T>(this IHealthChecksBuilder builder,
        HealthStatus? failureStatus = default, IEnumerable<string>? tags = default) => builder
        .Add(BackgroundQueueService<T>.ConsumerGroupReadinessCheck(failureStatus, tags));

    public static IHealthChecksBuilder AddBackgroundQueueLivenessCheck<T>(this IHealthChecksBuilder builder,
        HealthStatus? failureStatus = default, IEnumerable<string>? tags = default) => builder
        .Add(BackgroundQueueService<T>.ConsumerGroupLivenessCheck(failureStatus, tags));
}
