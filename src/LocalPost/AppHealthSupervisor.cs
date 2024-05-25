using System.Collections.Immutable;
using JetBrains.Annotations;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LocalPost;

[UsedImplicitly]
internal sealed class AppHealthSupervisor(ILogger<AppHealthSupervisor> logger,
    HealthCheckService healthChecker, IHostApplicationLifetime appLifetime) : IBackgroundService
{
    public TimeSpan CheckInterval { get; init; } = TimeSpan.FromSeconds(1);
    public int ExitCode { get; init; } = 1;
    public IImmutableSet<string> Tags { get; init; } = ImmutableHashSet<string>.Empty;

    public Task StartAsync(CancellationToken ct) => Task.CompletedTask;

    public async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var result = await Check(ct);
            if (result.Status == HealthStatus.Unhealthy)
            {
                logger.LogError("Health check failed, stopping the application...");
                appLifetime.StopApplication();
                Environment.ExitCode = ExitCode;
                break;
            }

            await Task.Delay(CheckInterval, ct);
        }
    }

    private Task<HealthReport> Check(CancellationToken ct = default) => Tags.Count == 0
        ? healthChecker.CheckHealthAsync(ct)
        : healthChecker.CheckHealthAsync(hcr => Tags.IsSubsetOf(hcr.Tags), ct);

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
