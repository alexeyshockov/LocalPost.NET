using System.Collections.Immutable;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace LocalPost.DependencyInjection;

internal interface IHealthAwareService
{
    IHealthCheck ReadinessCheck { get; }
}

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

        services.AddHostedService<AppHealthSupervisor>();

        return services;
    }
}

internal static partial class HealthChecks
{
    public static HealthCheckRegistration Readiness<T>(string name,
        HealthStatus? failureStatus = null, IEnumerable<string>? tags = null)
        where T : IHealthAwareService =>
        Readiness(typeof(T), name, failureStatus, tags);

    public static HealthCheckRegistration Readiness(Type bqService, string name,
        HealthStatus? failureStatus = null, IEnumerable<string>? tags = null) =>
        new(name, // Can be overwritten later
            provider => ((IHealthAwareService)provider.GetRequiredKeyedService(bqService, name)).ReadinessCheck,
            failureStatus, // Can be overwritten later
            tags);
}
