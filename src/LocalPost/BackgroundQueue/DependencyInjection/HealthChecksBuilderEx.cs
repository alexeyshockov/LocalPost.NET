using LocalPost.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LocalPost.BackgroundQueue.DependencyInjection;

[PublicAPI]
public static class HealthChecksBuilderEx
{
    public static IHealthChecksBuilder AddBackgroundQueue<T>(this IHealthChecksBuilder builder,
        string name, HealthStatus? failureStatus = null, IEnumerable<string>? tags = null) =>
        builder.Add(HealthChecks.Readiness<BackgroundQueue<T>>(name, failureStatus, tags));

    public static IHealthChecksBuilder AddKafkaConsumers(this IHealthChecksBuilder builder,
        HealthStatus? failureStatus = null, IEnumerable<string>? tags = null)
    {
        var services = builder.Services
            .Where(service => service is { IsKeyedService: true, ServiceType.IsGenericType: true } &&
                              service.ServiceType.GetGenericTypeDefinition() == typeof(BackgroundQueue<>))
            .Select(service => (service.ServiceType, (string)service.ServiceKey!));

        foreach (var (bqService, name) in services)
            builder.Add(HealthChecks.Readiness(bqService, name, failureStatus, tags));

        return builder;
    }
}
