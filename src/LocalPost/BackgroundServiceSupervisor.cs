using System.Diagnostics.CodeAnalysis;
using LocalPost.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using static Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult;

namespace LocalPost;

internal abstract class BackgroundServiceSupervisor : IHostedService, INamedService, IDisposable
{
    public sealed class LivenessCheck : IHealthCheck
    {
        private readonly BackgroundServiceSupervisor _supervisor;

        public LivenessCheck(BackgroundServiceSupervisor supervisor)
        {
            _supervisor = supervisor;
        }

        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
            CancellationToken cancellationToken = default) => Task.FromResult(CheckHealth(context));

        private HealthCheckResult CheckHealth(HealthCheckContext _)
        {
            if (_supervisor.Crashed)
                return Unhealthy($"{_supervisor.Name} has crashed", _supervisor.Exception);

            if (_supervisor is { Started: true, Running: false })
                return Unhealthy($"{_supervisor.Name} is not running");

            // Starting or running
            return Healthy($"{_supervisor.Name} is alive");
        }
    }

    public sealed class ReadinessCheck : IHealthCheck
    {
        private readonly BackgroundServiceSupervisor _supervisor;

        public ReadinessCheck(BackgroundServiceSupervisor supervisor)
        {
            _supervisor = supervisor;
        }

        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
            CancellationToken cancellationToken = default) => Task.FromResult(CheckHealth(context));

        private HealthCheckResult CheckHealth(HealthCheckContext context)
        {
            if (!_supervisor.Started)
                return Unhealthy($"{_supervisor.Name} has not been started yet", _supervisor.Exception);

            if (_supervisor.Crashed)
                return Unhealthy($"{_supervisor.Name} has crashed", _supervisor.Exception);

            return Healthy($"{_supervisor.Name} is running");
        }
    }

    private readonly ILogger<BackgroundServiceSupervisor> _logger;

    private CancellationTokenSource? _executionCts;
    private Task? _execution;

    public BackgroundServiceSupervisor(ILogger<BackgroundServiceSupervisor> logger, IBackgroundService service)
    {
        _logger = logger;
        Service = service;
    }

    public IBackgroundService Service { get; }

    public string Name => Service.Name;

    public bool Started => _executionCts is not null && _execution is not null;

    public bool Running => _execution is not null && _execution.IsCompleted;

    [MemberNotNullWhen(true, nameof(Exception))]
    public bool Crashed => Exception is not null;

    public Exception? Exception { get; private set; }

    public async Task StartAsync(CancellationToken ct)
    {
        if (_executionCts is not null)
            throw new InvalidOperationException("Service has been already started");

        _executionCts = new CancellationTokenSource();

        try
        {
            await Service.StartAsync(ct);

            // Store the task we're executing
            _execution = ExecuteAsync(_executionCts.Token);
        }
        catch (OperationCanceledException e) when (e.CancellationToken == ct)
        {
            _logger.LogInformation("{Name} start has been aborted", Name);
        }
        catch (Exception e)
        {
            Exception = e;
            _logger.LogCritical(e, "Unhandled exception while starting {Name} background queue", Name);
        }
    }

    private async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // In case stop has been already requested
        if (stoppingToken.IsCancellationRequested)
            return;

        try
        {
            await Service.ExecuteAsync(stoppingToken);
            _logger.LogInformation("{Name} background queue is completed", Name);
        }
        catch (OperationCanceledException e) when (e.CancellationToken == stoppingToken)
        {
            // The rest of the queue will be processed in StopAsync() below
            _logger.LogInformation("Application exit has been requested, stopping {Name} background queue...", Name);
        }
        catch (Exception e)
        {
            Exception = e;
            _logger.LogCritical(e, "Unhandled exception in {Name} background queue", Name);
        }
    }

    public async Task StopAsync(CancellationToken forceExitToken)
    {
        try
        {
            // Signal cancellation to the executing method
            _executionCts?.Cancel();
        }
        finally
        {
            if (_execution is not null)
                // Wait until the execution completes or the app is forced to exit
                await Task.WhenAny(_execution, Task.Delay(Timeout.Infinite, forceExitToken));
        }

        await Service.StopAsync(forceExitToken);
    }

    public void Dispose()
    {
        _executionCts?.Cancel();
        // ReSharper disable once SuspiciousTypeConversion.Global
        if (Service is IDisposable disposableService)
            disposableService.Dispose();
    }
}

internal sealed class BackgroundServiceSupervisor<T> : BackgroundServiceSupervisor
    where T : class, IBackgroundService
{
    public BackgroundServiceSupervisor(ILogger<BackgroundServiceSupervisor<T>> logger, T service) : base(logger, service)
    {
        Service = service;
    }

    public new T Service { get; }
}
