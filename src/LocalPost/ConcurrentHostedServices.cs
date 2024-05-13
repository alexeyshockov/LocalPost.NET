using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using LocalPost.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Nito.AsyncEx;
using static Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult;

namespace LocalPost;

internal interface IBackgroundService
{
    Task StartAsync(CancellationToken ct);

    Task ExecuteAsync(CancellationToken ct);

    Task StopAsync(CancellationToken ct);
}

internal interface IBackgroundServiceMonitor
{
    public sealed class LivenessCheck : IHealthCheck
    {
        public required IBackgroundServiceMonitor Service { get; init; }

        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
            CancellationToken cancellationToken = default) => Task.FromResult(CheckHealth(context));

        private HealthCheckResult CheckHealth(HealthCheckContext _) => Service switch
        {
            { Crashed: true } => Unhealthy("Crashed", Service.Exception),
//            { Running: false } => Degraded("Not (yet) running"),
            // Started and running
            _ => Healthy("Alive")
        };
    }

    // Readiness like "ready to handle requests" is the same a liveness check here. At least at the moment.
    public sealed class ReadinessCheck : IHealthCheck
    {
        public required IBackgroundServiceMonitor Service { get; init; }

        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
            CancellationToken cancellationToken = default) => Task.FromResult(CheckHealth(context));

        private HealthCheckResult CheckHealth(HealthCheckContext _) => Service switch
        {
            { Running: true } => Healthy("Running"),
            _ => Unhealthy("Not (yet) running")
        };
    }

    public bool Started { get; }

    public bool Running { get; }

    [MemberNotNullWhen(true, nameof(Exception))]
    public bool Crashed { get; }

    public Exception? Exception { get; }
}

internal class NamedBackgroundServiceRunner<T> : BackgroundServiceRunner<T>, INamedService
    where T : class, IBackgroundService, INamedService
{
    public NamedBackgroundServiceRunner(T service, IHostApplicationLifetime appLifetime) : base(service, appLifetime)
    {
        Name = service.Name;
    }

    public string Name { get; }
}

internal class BackgroundServiceRunner<T> : IConcurrentHostedService, IBackgroundServiceMonitor, IDisposable
    where T : class, IBackgroundService
{
    private Task? _start;
    private CancellationTokenSource? _executionCts;
    private Task? _execution;

    private readonly T _service;
    private readonly IHostApplicationLifetime _appLifetime;

    public BackgroundServiceRunner(T service, IHostApplicationLifetime appLifetime)
    {
        _service = service;
        _appLifetime = appLifetime;
    }

    public bool Starting => _start is not null && !_start.IsCompleted;

    // StartedSuccessfully?..
    public bool Started => _start is not null && _start.Status == TaskStatus.RanToCompletion;

    public bool Running => _execution is not null && !_execution.IsCompleted;

    public bool StartCrashed => _start is not null && _start.Status == TaskStatus.Faulted;
    public bool RunCrashed => _execution is not null && _execution.Status == TaskStatus.Faulted;
    public bool Crashed => StartCrashed || RunCrashed;

    // TODO Test
    public Exception? Exception => (StartCrashed ? _start?.Exception : _execution?.Exception)?.InnerException;

    private async Task WaitAppStartAsync(CancellationToken ct)
    {
        try
        {
            // Wait until all other services are started
            await Task.Delay(Timeout.Infinite, _appLifetime.ApplicationStarted).WaitAsync(ct);
        }
        catch (OperationCanceledException e) when (e.CancellationToken == _appLifetime.ApplicationStarted)
        {
            // Startup completed, continue
        }
    }

    public async Task StartAsync(CancellationToken ct)
    {
        // All the services are started from the same (main) thread, so there are no races
        if (_start is not null)
            throw new InvalidOperationException("Service is already started");

        await (_start = _service.StartAsync(ct));

        // Start execution in the background...
#pragma warning disable CS4014
        ExecuteAsync();
#pragma warning restore CS4014
    }

    private async Task ExecuteAsync()
    {
        _executionCts = new CancellationTokenSource();
        var ct = _executionCts.Token;

        try
        {
            await WaitAppStartAsync(ct);
            await (_execution = _service.ExecuteAsync(ct));
        }
        catch (OperationCanceledException e) when (e.CancellationToken == ct)
        {
            // Normal case, we trigger this token ourselves when stopping the service
        }
        catch (Exception)
        {
            // Otherwise it's an error, but swallow it silently (this method is called in "fire and forget" mode, not
            // awaited, so any unhandled exception will arrive in TaskScheduler.UnobservedTaskException, which is not
            // what we want).
            // See also: https://stackoverflow.com/a/59300076/322079.
        }
    }

    public async Task StopAsync(CancellationToken forceExitToken)
    {
        if (_executionCts is null)
            // Or simply ignore and return?..
            throw new InvalidOperationException("Service has not been started");

        if (!_executionCts.IsCancellationRequested)
            _executionCts.Cancel(); // Signal cancellation to the service

        if (_execution is not null)
            // Wait until the execution completes or the app is forced to exit
            await _execution.WaitAsync(forceExitToken);

        await _service.StopAsync(forceExitToken);
    }

    public void Dispose()
    {
        _executionCts?.Dispose();
    }
}

internal interface IConcurrentHostedService : IHostedService
{
}

internal sealed class ConcurrentHostedServices : IHostedService
{
    private readonly ImmutableArray<IConcurrentHostedService> _services;

    public ConcurrentHostedServices(IEnumerable<IConcurrentHostedService> services)
    {
        _services = services.ToImmutableArray();
    }

    public Task StartAsync(CancellationToken cancellationToken) =>
        Task.WhenAll(_services.Select(c => c.StartAsync(cancellationToken)));

    public Task StopAsync(CancellationToken cancellationToken) =>
        Task.WhenAll(_services.Select(c => c.StopAsync(cancellationToken)));
}
