using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nito.AsyncEx;
using static Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult;

namespace LocalPost;

internal sealed record CombinedHostedService(ImmutableArray<IHostedService> Services) : IHostedService
{
    public CombinedHostedService(IHostedService s1, IHostedService s2) : this(new[] { s1, s2 })
    {
    }

    public CombinedHostedService(IEnumerable<IHostedService> services) : this(services.ToImmutableArray())
    {
    }

    public Task StartAsync(CancellationToken cancellationToken) =>
        Task.WhenAll(Services.Select(c => c.StartAsync(cancellationToken)));

    public Task StopAsync(CancellationToken cancellationToken) =>
        Task.WhenAll(Services.Select(c => c.StopAsync(cancellationToken)));

}

internal interface IBackgroundServiceSupervisor : IHostedService
{
    // With predefined static size
    // (IAsyncDisposable later?..)
    internal sealed record Combined(ImmutableArray<IBackgroundServiceSupervisor> Supervisors) :
        IBackgroundServiceSupervisor, IDisposable
    {
        public Combined(IEnumerable<IBackgroundServiceSupervisor> supervisors) : this(supervisors.ToImmutableArray())
        {
        }

        public Task StartAsync(CancellationToken cancellationToken) =>
            Task.WhenAll(Supervisors.Select(c => c.StartAsync(cancellationToken)));

        public Task StopAsync(CancellationToken cancellationToken) =>
            Task.WhenAll(Supervisors.Select(c => c.StopAsync(cancellationToken)));

        public bool Started => Supervisors.All(c => c.Started);
        public bool Running => Supervisors.All(c => c.Running);
        public bool Crashed => Supervisors.Any(c => c.Crashed);
        public Exception? Exception => null; // TODO Implement

        public void Dispose()
        {
            foreach (var consumer in Supervisors)
                if (consumer is IDisposable disposable)
                    disposable.Dispose();
        }
    }

    public bool Started { get; }

    public bool Running { get; }

    [MemberNotNullWhen(true, nameof(Exception))]
    public bool Crashed { get; }

    public Exception? Exception { get; }

    public sealed class LivenessCheck : IHealthCheck
    {
        private readonly IBackgroundServiceSupervisor _supervisor;

        public LivenessCheck(IBackgroundServiceSupervisor supervisor)
        {
            _supervisor = supervisor;
        }

        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
            CancellationToken cancellationToken = default) => Task.FromResult(CheckHealth(context));

        private HealthCheckResult CheckHealth(HealthCheckContext _)
        {
            if (_supervisor.Crashed)
                return Unhealthy("Service has crashed", _supervisor.Exception);

            if (_supervisor is { Started: true, Running: false })
                return Unhealthy("Service is not running");

            // Starting or running
            return Healthy("Service is alive");
        }
    }

    public sealed class ReadinessCheck : IHealthCheck
    {
        private readonly IBackgroundServiceSupervisor _supervisor;

        public ReadinessCheck(IBackgroundServiceSupervisor supervisor)
        {
            _supervisor = supervisor;
        }

        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
            CancellationToken cancellationToken = default) => Task.FromResult(CheckHealth(context));

        private HealthCheckResult CheckHealth(HealthCheckContext context)
        {
            if (!_supervisor.Started)
                return Unhealthy("Service has not been started yet", _supervisor.Exception);

            if (_supervisor.Crashed)
                return Unhealthy("Service has crashed", _supervisor.Exception);

            return Healthy("Service is running");
        }
    }
}

internal class BackgroundServiceSupervisor : IBackgroundServiceSupervisor, IDisposable
{
    private readonly ILogger<BackgroundServiceSupervisor> _logger;

    private CancellationTokenSource? _executionCts;
    private Task? _execution;

    public BackgroundServiceSupervisor(ILogger<BackgroundServiceSupervisor> logger, string name,
        IBackgroundService service)
    {
        _logger = logger;
        Name = name;
        Service = service;
    }

    public string Name { get; }

    public IBackgroundService Service { get; }

    public bool Started => _executionCts is not null && _execution is not null;

    public bool Running => _execution is not null && !_execution.IsCompleted;

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
        catch (Exception e)
        {
            Exception = e;
            _logger.LogCritical(e, "Unhandled exception while starting {Name} service", Name);
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
            _logger.LogWarning("{Name} is done", Name);
        }
        catch (OperationCanceledException e) when (e.CancellationToken == stoppingToken)
        {
            // The rest of the queue will be processed in StopAsync() below
            _logger.LogInformation("Application exit has been requested, stopping {Name}...", Name);
        }
        catch (Exception e)
        {
            Exception = e;
            _logger.LogCritical(e, "{Name}: Unhandled exception", Name);
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
            // Wait until the execution completes or the app is forced to exit
            _execution?.WaitAsync(forceExitToken);
        }

        await Service.StopAsync(forceExitToken);
        _logger.LogInformation("{Name} has been stopped", Name);
    }

    public void Dispose()
    {
        _executionCts?.Cancel();
        _executionCts?.Dispose();
        // ReSharper disable once SuspiciousTypeConversion.Global
        if (Service is IDisposable disposableService)
            disposableService.Dispose();
    }
}

//internal sealed class BackgroundServiceSupervisor<T> : BackgroundServiceSupervisor
//    where T : class, IBackgroundService, INamedService
//{
//    public BackgroundServiceSupervisor(ILogger<BackgroundServiceSupervisor<T>> logger, T service) :
//        base(logger, service.Name, service)
//    {
//        Service = service;
//    }
//
//    public new T Service { get; }
//}
