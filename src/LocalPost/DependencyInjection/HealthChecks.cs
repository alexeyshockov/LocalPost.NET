using System.Collections.Immutable;
using JetBrains.Annotations;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LocalPost.DependencyInjection;

[PublicAPI]
public static partial class ServiceCollectionEx
{
    public static IServiceCollection AddAppHealthSupervisor(this IServiceCollection services,
        IEnumerable<string>? tags = null)
    {
        services.AddSingleton<AppHealthSupervisor>(provider => new AppHealthSupervisor(
            provider.GetRequiredService<ILogger<AppHealthSupervisor>>(),
            provider.GetRequiredService<HealthCheckService>(),
            provider.GetRequiredService<IHostApplicationLifetime>())
        {
            Tags = tags?.ToImmutableHashSet() ?? ImmutableHashSet<string>.Empty
        });

        services.AddBackgroundServiceFor<AppHealthSupervisor>();

        return services;
    }
}

internal static class HealthChecksBuilderEx
{
    internal static IHealthChecksBuilder AddConsumerLivenessCheck<TQ, T>(this IHealthChecksBuilder builder,
        string? name = default, HealthStatus? failureStatus = default, IEnumerable<string>? tags = default)
        where TQ : IAsyncEnumerable<T>
    {
        var check = HealthChecks.LivenessCheckFor<Queue.Consumer<TQ, T>>(failureStatus, tags);
        if (name is not null)
            check.Name = name;

        return builder.Add(check);
    }

    internal static IHealthChecksBuilder AddNamedConsumerLivenessCheck<TQ, T>(this IHealthChecksBuilder builder,
        string name, HealthStatus? failureStatus = default, IEnumerable<string>? tags = default)
        where TQ : IAsyncEnumerable<T>, INamedService
    {
        var check = HealthChecks.LivenessCheckForNamed<Queue.NamedConsumer<TQ, T>>(name, failureStatus, tags);

        return builder.Add(check);
    }
}

internal static class HealthChecks
{
    public static HealthCheckRegistration LivenessCheckFor<T>(
        HealthStatus? failureStatus = null, IEnumerable<string>? tags = null)
        where T : class, IBackgroundService =>
        new(Reflection.FriendlyNameOf<T>(), // Can be overwritten later
            provider => new IBackgroundServiceMonitor.LivenessCheck
                { Service = provider.GetRequiredService<BackgroundServiceRunner<T>>() },
            failureStatus, // Can be overwritten later
            tags);

    public static HealthCheckRegistration LivenessCheckForNamed<T>(string name,
        HealthStatus? failureStatus = null, IEnumerable<string>? tags = null)
        where T : class, IBackgroundService, INamedService =>
        new(name, // Can be overwritten later
            provider => new IBackgroundServiceMonitor.LivenessCheck
                { Service = provider.GetRequiredService<NamedBackgroundServiceRunner<T>>(name) },
            failureStatus, // Can be overwritten later
            tags);

    public static HealthCheckRegistration ReadinessCheckForNamed<T>(
        string name, HealthStatus? failureStatus = null, IEnumerable<string>? tags = null)
        where T : class, IBackgroundService, INamedService =>
        new(name, // Can be overwritten later
            provider => new IBackgroundServiceMonitor.ReadinessCheck
                { Service = provider.GetRequiredService<NamedBackgroundServiceRunner<T>>(name) },
            failureStatus, // Can be overwritten later
            tags);
}
