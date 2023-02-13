using LocalPost.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LocalPost.DependencyInjection;

public static class HealthChecks
{
    public static IHealthChecksBuilder AddBackgroundQueueConsumerReadinessCheck<T>(this IHealthChecksBuilder builder,
        string name, HealthStatus? failureStatus = default, IEnumerable<string>? tags = default,
        TimeSpan? timeout = default) =>
        builder.AddBackgroundServiceReadinessCheck<BackgroundQueueConsumer<T>>(name, failureStatus, tags, timeout);

    public static IHealthChecksBuilder AddBackgroundQueueConsumerLivenessCheck<T>(this IHealthChecksBuilder builder,
        string name, HealthStatus? failureStatus = default, IEnumerable<string>? tags = default,
        TimeSpan? timeout = default) =>
        builder.AddBackgroundServiceLivenessCheck<BackgroundQueueConsumer<T>>(name, failureStatus, tags, timeout);

    internal static IHealthChecksBuilder AddBackgroundServiceReadinessCheck<T>(this IHealthChecksBuilder builder,
        string name, HealthStatus? failureStatus = default, IEnumerable<string>? tags = default,
        TimeSpan? timeout = default) where T : class, IBackgroundService =>
        builder.Add(new HealthCheckRegistration(
            name,
            provider => ActivatorUtilities.CreateInstance<BackgroundServiceSupervisor<T>.ReadinessCheck>(provider,
                provider.GetSupervisor<T>(name)),
            failureStatus,
            tags,
            timeout));

    internal static IHealthChecksBuilder AddBackgroundServiceLivenessCheck<T>(this IHealthChecksBuilder builder,
        string name, HealthStatus? failureStatus = default, IEnumerable<string>? tags = default,
        TimeSpan? timeout = default) where T : class, IBackgroundService =>
        builder.Add(new HealthCheckRegistration(
            name,
            provider => ActivatorUtilities.CreateInstance<BackgroundServiceSupervisor<T>.LivenessCheck>(provider,
                provider.GetSupervisor<T>(name)),
            failureStatus,
            tags,
            timeout));
}
