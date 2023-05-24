using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Nito.AsyncEx;
using static Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult;

namespace LocalPost;

internal interface IBackgroundServiceSupervisor : IConcurrentHostedService, IDisposable
{
    // With predefined static size
    // (IAsyncDisposable later?..)
    internal sealed record Combined(ImmutableArray<IBackgroundServiceSupervisor> Supervisors) :
        IBackgroundServiceSupervisor
    {
        public Combined(IBackgroundServiceSupervisor s1, IBackgroundServiceSupervisor s2) : this(new[] { s1, s2 })
        {
        }

        public Combined(IEnumerable<IBackgroundServiceSupervisor> supervisors) : this(supervisors.ToImmutableArray())
        {
        }

        public Task StartAsync(CancellationToken ct) =>
            Task.WhenAll(Supervisors.Select(service => service.StartAsync(ct)));

        public Task StopAsync(CancellationToken ct) =>
            Task.WhenAll(Supervisors.Select(service => service.StopAsync(ct)));

        public bool Started => Supervisors.All(c => c.Started);
        public bool Running => Supervisors.All(c => c.Running);
        public bool Crashed => Supervisors.Any(c => c.Crashed);
        public Exception? Exception => null; // TODO Implement

        public void Dispose()
        {
            foreach (var disposable in Supervisors)
                disposable.Dispose();
        }
    }

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
                return Unhealthy("Crashed", _supervisor.Exception);

            if (_supervisor is { Started: true, Running: false })
                return Unhealthy("Not running");

            // Starting or running
            return Healthy("Alive");
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
                return Unhealthy("Has not been started yet", _supervisor.Exception);

            if (_supervisor.Crashed)
                return Unhealthy("Crashed", _supervisor.Exception);

            return Healthy("Running or completed");
        }
    }

    public bool Started { get; }

    public bool Running { get; }

    [MemberNotNullWhen(true, nameof(Exception))]
    public bool Crashed { get; }

    public Exception? Exception { get; }
}

internal sealed class BackgroundServiceSupervisor : IBackgroundServiceSupervisor
{
    private CancellationTokenSource? _executionCts;
    private Task? _execution;

    public BackgroundServiceSupervisor(IBackgroundService service)
    {
        Service = service;
    }

    public IBackgroundService Service { get; }

    // TODO StartedSuccessfully
    public bool Started => _executionCts is not null && _execution is not null;

    public bool Running => _execution is not null && !_execution.IsCompleted;

    public bool Crashed => Exception is not null;

    public Exception? Exception { get; private set; }

    public async Task StartAsync(CancellationToken ct)
    {
        if (_executionCts is not null)
            throw new InvalidOperationException("Execution has been already started");

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
        }
    }

    private async Task ExecuteAsync(CancellationToken ct)
    {
        if (ct.IsCancellationRequested)
            return;

        try
        {
            await Service.ExecuteAsync(ct);

            // Completed
        }
        catch (OperationCanceledException e) when (e.CancellationToken == ct)
        {
            // Completed gracefully on request
        }
        catch (Exception e)
        {
            Exception = e;
        }
    }

    public async Task StopAsync(CancellationToken forceExitToken)
    {
        if (_executionCts is null || _executionCts.IsCancellationRequested)
            return;

        // Signal cancellation to the executing method
        _executionCts.Cancel();

        if (_execution is null)
            return;

        // Wait until the execution completes or the app is forced to exit
        await _execution.WaitAsync(forceExitToken);

        await Service.StopAsync(forceExitToken);
    }

    public void Dispose()
    {
        _executionCts?.Dispose();
        // ReSharper disable once SuspiciousTypeConversion.Global
        if (Service is IDisposable disposable)
            disposable.Dispose();
    }
}
