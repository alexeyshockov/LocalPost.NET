using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LocalPost.DependencyInjection;

[PublicAPI]
public static class ServiceHealthCheckRegistration
{
    // Not needed, as there is no complex logic inside. It's either working, or dead.
//    public static IHealthChecksBuilder AddBackgroundQueueReadinessCheck<T>(...

    public static IHealthChecksBuilder AddBackgroundQueueLivenessCheck<T>(this IHealthChecksBuilder builder,
        HealthStatus? failureStatus = default, IEnumerable<string>? tags = default) => builder
        .AddConsumerGroupLivenessCheck<BackgroundQueue<T, T>, T>();
}
