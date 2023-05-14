using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LocalPost.DependencyInjection;

public static class HealthChecks
{
    [SuppressMessage("ReSharper", "PossibleMultipleEnumeration")]
    public static IHealthChecksBuilder AddBackgroundQueueReadinessCheck<T>(this IHealthChecksBuilder builder,
        string name, HealthStatus? failureStatus = default, IEnumerable<string>? tags = default,
        TimeSpan? timeout = default) => builder
        .AddBackgroundServiceReadinessCheck<Consumer.Service>(name, failureStatus, tags, timeout)
        .AddBackgroundQueueConsumerReadinessCheck<ConsumeContext>(name, failureStatus, tags, timeout);

    [SuppressMessage("ReSharper", "PossibleMultipleEnumeration")]
    public static IHealthChecksBuilder AddBackgroundQueueLivenessCheck<T>(this IHealthChecksBuilder builder,
        string name, HealthStatus? failureStatus = default, IEnumerable<string>? tags = default,
        TimeSpan? timeout = default) => builder
        .AddBackgroundServiceLivenessCheck<Consumer.Service>(name, failureStatus, tags, timeout)
        .AddBackgroundQueueConsumerLivenessCheck<ConsumeContext>(name, failureStatus, tags, timeout);
}

public static class HealthCheckBuilderEx
{
    internal static IHealthChecksBuilder AddBackgroundQueueConsumerReadinessCheck<T>(this IHealthChecksBuilder builder,
        string name, HealthStatus? failureStatus = default, IEnumerable<string>? tags = default,
        TimeSpan? timeout = default) =>
        builder.AddBackgroundServiceReadinessCheck<BackgroundQueueConsumer<T>>(name, failureStatus, tags, timeout);

    internal static IHealthChecksBuilder AddBackgroundQueueConsumerLivenessCheck<T>(this IHealthChecksBuilder builder,
        string name, HealthStatus? failureStatus = default, IEnumerable<string>? tags = default,
        TimeSpan? timeout = default) =>
        builder.AddBackgroundServiceLivenessCheck<BackgroundQueueConsumer<T>>(name, failureStatus, tags, timeout);

    internal static IHealthChecksBuilder AddBackgroundServiceReadinessCheck<T>(this IHealthChecksBuilder builder,
        string name, HealthStatus? failureStatus = default, IEnumerable<string>? tags = default,
        TimeSpan? timeout = default) where T : class, IBackgroundService =>
        builder.Add(new HealthCheckRegistration(
            name,
            provider => ActivatorUtilities.CreateInstance<BackgroundServiceSupervisor.ReadinessCheck>(provider,
                provider.GetSupervisor<T>(name)),
            failureStatus,
            tags,
            timeout));

    internal static IHealthChecksBuilder AddBackgroundServiceLivenessCheck<T>(this IHealthChecksBuilder builder,
        string name, HealthStatus? failureStatus = default, IEnumerable<string>? tags = default,
        TimeSpan? timeout = default) where T : class, IBackgroundService =>
        builder.Add(new HealthCheckRegistration(
            name,
            provider => ActivatorUtilities.CreateInstance<BackgroundServiceSupervisor.LivenessCheck>(provider,
                provider.GetSupervisor<T>(name)),
            failureStatus,
            tags,
            timeout));
}
