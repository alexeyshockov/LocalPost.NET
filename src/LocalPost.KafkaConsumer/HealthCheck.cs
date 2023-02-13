using Microsoft.Extensions.Diagnostics.HealthChecks;
using static Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult;

namespace LocalPost.KafkaConsumer;

internal class HealthCheck<TKey, TValue> : IHealthCheck
{
    private readonly Consumer<TKey, TValue>.Service _consumer;


    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = default) => Task.FromResult(_currentRates.UpdatedAt switch
    {
        not null => Healthy("Thresholds loaded", new Dictionary<string, object>
        {
            ["LastUpdated"] = _currentRates.UpdatedAt,
            ["RulesThreshold"] = _currentRates.Rules,
            ["WorkflowThreshold"] = _currentRates.Workflows
        }),
        _ => Unhealthy("Thresholds not loaded")
    });
}
