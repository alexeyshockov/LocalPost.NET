using System.Collections.Immutable;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace LocalPost.DependencyInjection;

[PublicAPI]
public static partial class ServiceCollectionEx
{
    public static IServiceCollection AddAppHealthSupervisor(this IServiceCollection services,
        IEnumerable<string>? tags = null)
    {
        services.AddSingleton<AppHealthSupervisor>(provider => new AppHealthSupervisor(
            provider.GetLoggerFor<AppHealthSupervisor>(),
            provider.GetRequiredService<HealthCheckService>(),
            provider.GetRequiredService<IHostApplicationLifetime>())
        {
            Tags = tags?.ToImmutableHashSet() ?? ImmutableHashSet<string>.Empty
        });

        services.AddBackgroundService<AppHealthSupervisor>();

        return services;
    }
}

internal static class HealthChecksBuilderEx
{
    internal static IHealthChecksBuilder AddPipelineLivenessCheck<T>(this IHealthChecksBuilder builder,
        HealthStatus? failureStatus = default, IEnumerable<string>? tags = default)
    {
        var check = HealthChecks.PipelineLivenessCheckFor<T>(failureStatus, tags);
        // if (name is not null)
        //     check.Name = name;

        return builder.Add(check);
    }

    internal static IHealthChecksBuilder AddPipelineLivenessCheck<T>(this IHealthChecksBuilder builder, string name,
        HealthStatus? failureStatus = default, IEnumerable<string>? tags = default)
        where T : INamedService
    {
        var check = HealthChecks.PipelineLivenessCheckFor<T>(name, failureStatus, tags);

        return builder.Add(check);
    }
}

internal static class HealthChecks
{
    public static HealthCheckRegistration LivenessCheck<T>(
        HealthStatus? failureStatus = null, IEnumerable<string>? tags = null)
        where T : class, IBackgroundService =>
        new(Reflection.FriendlyNameOf<T>(), // Can be overwritten later
            provider => new IBackgroundServiceMonitor.LivenessCheck
                { Service = provider.GetBackgroundServiceRunner<T>() },
            failureStatus, // Can be overwritten later
            tags);

    public static HealthCheckRegistration LivenessCheck<T>(string name,
        HealthStatus? failureStatus = null, IEnumerable<string>? tags = null)
        where T : class, IBackgroundService, INamedService =>
        new(name, // Can be overwritten later
            provider => new IBackgroundServiceMonitor.LivenessCheck
                { Service = provider.GetBackgroundServiceRunner<T>(name) },
            failureStatus, // Can be overwritten later
            tags);

    public static HealthCheckRegistration ReadinessCheck<T>(string name,
        HealthStatus? failureStatus = null, IEnumerable<string>? tags = null)
        where T : class, IBackgroundService, INamedService =>
        new(name, // Can be overwritten later
            provider => new IBackgroundServiceMonitor.ReadinessCheck
                { Service = provider.GetBackgroundServiceRunner<T>(name) },
            failureStatus, // Can be overwritten later
            tags);

    // public static HealthCheckRegistration LivenessCheckFor<T>(string target,
    //     HealthStatus? failureStatus = null, IEnumerable<string>? tags = null)
    //     where T : class, IBackgroundService, IAssistantService =>
    //     new(target, // Can be overwritten later
    //         provider => new IBackgroundServiceMonitor.LivenessCheck
    //             { Service = provider.GetBackgroundServiceRunnerFor<T>(target) },
    //         failureStatus, // Can be overwritten later
    //         tags);
    //
    // public static HealthCheckRegistration ReadinessCheckFor<T>(string target,
    //     HealthStatus? failureStatus = null, IEnumerable<string>? tags = null)
    //     where T : class, IBackgroundService, IAssistantService =>
    //     new(target, // Can be overwritten later
    //         provider => new IBackgroundServiceMonitor.ReadinessCheck
    //             { Service = provider.GetBackgroundServiceRunnerFor<T>(target) },
    //         failureStatus, // Can be overwritten later
    //         tags);

    public static HealthCheckRegistration PipelineLivenessCheckFor<T>(
        HealthStatus? failureStatus = null, IEnumerable<string>? tags = null) =>
        // TODO Make it like "MessageSource:pipeline"...
        new(Reflection.FriendlyNameOf<T>(), // Can be overwritten later
            provider => new IBackgroundServiceMonitor.LivenessCheck
                { Service = provider.GetPipelineMonitorFor<T>() },
            failureStatus, // Can be overwritten later
            tags);

    public static HealthCheckRegistration PipelineLivenessCheckFor<T>(string name,
        HealthStatus? failureStatus = null, IEnumerable<string>? tags = null)
        where T : INamedService =>
        // TODO Make it like "MessageSource:pipeline:name"...
        new(Reflection.FriendlyNameOf<T>(name), // Can be overwritten later
            provider => new IBackgroundServiceMonitor.LivenessCheck
                { Service = provider.GetPipelineMonitorFor<T>(name) },
            failureStatus, // Can be overwritten later
            tags);

    // public static HealthCheckRegistration PipelineReadinessCheckFor<T>(string name,
    //     HealthStatus? failureStatus = null, IEnumerable<string>? tags = null) =>
    //     new(AssistedService.From<T>(), // Can be overwritten later
    //         provider => new IBackgroundServiceMonitor.ReadinessCheck
    //             { Service = provider.GetPipelineMonitorFor<T>(name) },
    //         failureStatus, // Can be overwritten later
    //         tags);
}
