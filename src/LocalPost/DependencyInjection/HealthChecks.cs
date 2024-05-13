using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;

namespace LocalPost.DependencyInjection;

internal static class HealthChecks
{
    public static HealthCheckRegistration LivenessCheckFor<T>(HealthStatus? failureStatus = null,
        IEnumerable<string>? tags = default) where T : class, IBackgroundService => new(
        Reflection.FriendlyNameOf<T>(), // Can be overwritten later
        provider => new IBackgroundServiceMonitor.LivenessCheck
            { Service = provider.GetRequiredService<BackgroundServiceRunner<T>>() },
        failureStatus, // Can be overwritten later
        tags);

    public static HealthCheckRegistration LivenessCheckForNamed<T>(string name, HealthStatus? failureStatus = null,
        IEnumerable<string>? tags = default) where T : class, IBackgroundService, INamedService => new(
        name, // Can be overwritten later
        provider => new IBackgroundServiceMonitor.LivenessCheck
            { Service = provider.GetRequiredService<NamedBackgroundServiceRunner<T>>(name) },
        failureStatus, // Can be overwritten later
        tags);

    public static HealthCheckRegistration ReadinessCheckForNamed<T>(string name, HealthStatus? failureStatus = null,
        IEnumerable<string>? tags = default) where T : class, IBackgroundService, INamedService => new(
        name, // Can be overwritten later
        provider => new IBackgroundServiceMonitor.ReadinessCheck
            { Service = provider.GetRequiredService<NamedBackgroundServiceRunner<T>>(name) },
        failureStatus, // Can be overwritten later
        tags);
}
