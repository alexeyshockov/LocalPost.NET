using System.Collections.Immutable;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace LocalPost;

[UsedImplicitly]
internal sealed class AppHealthSupervisor(ILogger<AppHealthSupervisor> logger,
    HealthCheckService healthChecker, IHostApplicationLifetime appLifetime) : BackgroundService
{
    public TimeSpan CheckInterval { get; init; } = TimeSpan.FromSeconds(1);
    public int ExitCode { get; init; } = 1;
    public IImmutableSet<string> Tags { get; init; } = ImmutableHashSet<string>.Empty;

    private Task<HealthReport> Check(CancellationToken ct = default) => Tags.Count == 0
        ? healthChecker.CheckHealthAsync(ct)
        : healthChecker.CheckHealthAsync(hcr => Tags.IsSubsetOf(hcr.Tags), ct);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var result = await Check(ct).ConfigureAwait(false);
            if (result.Status == HealthStatus.Unhealthy)
            {
                logger.LogError("Health check failed, stopping the application...");
                appLifetime.StopApplication();
                Environment.ExitCode = ExitCode;
                break;
            }

            await Task.Delay(CheckInterval, ct).ConfigureAwait(false);
        }
    }
}
