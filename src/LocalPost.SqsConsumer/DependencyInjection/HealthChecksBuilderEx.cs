using LocalPost.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LocalPost.SqsConsumer.DependencyInjection;

[PublicAPI]
public static class HealthChecksBuilderEx
{
    public static IHealthChecksBuilder AddSqsConsumer(this IHealthChecksBuilder builder,
        string name, HealthStatus? failureStatus = null, IEnumerable<string>? tags = null) =>
        builder.Add(HealthChecks.Readiness<Consumer>(name, failureStatus, tags));

    public static IHealthChecksBuilder AddSqsConsumers(this IHealthChecksBuilder builder,
        HealthStatus? failureStatus = null, IEnumerable<string>? tags = null)
    {
        foreach (var name in builder.Services.GetKeysFor<Consumer>().OfType<string>())
            AddSqsConsumer(builder, name, failureStatus, tags);

        return builder;
    }
}
