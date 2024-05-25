using JetBrains.Annotations;
using LocalPost.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LocalPost.BackgroundQueues.DependencyInjection;


[PublicAPI]
public static class HealthChecksBuilderEx
{
    // Not needed, as there is no complex logic inside. It's either working, or dead.
//    public static IHealthChecksBuilder AddBackgroundQueueReadinessCheck<T>(...

    public static IHealthChecksBuilder AddBackgroundQueueLivenessCheck<T>(this IHealthChecksBuilder builder,
        HealthStatus? failureStatus = default, IEnumerable<string>? tags = default) => builder
        .AddConsumerLivenessCheck<BackgroundQueue<T, T>, T>();
}
