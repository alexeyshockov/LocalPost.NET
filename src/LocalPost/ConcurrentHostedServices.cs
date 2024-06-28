using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using static Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult;

namespace LocalPost;

internal interface IBackgroundService
{
    Task StartAsync(CancellationToken ct);

    Task ExecuteAsync(CancellationToken ct);

    Task StopAsync(CancellationToken ct);
}

internal sealed class BackgroundServicesMonitor(IReadOnlyCollection<IBackgroundServiceMonitor> services)
    : IBackgroundServiceMonitor
{
    public bool Started => services.All(s => s.Started);
    public bool Running => services.All(s => s.Running);
    public Task Stopped => Task.WhenAll(services.Select(s => s.Stopped));

    public bool Crashed => services.Any(s => s.Crashed);

    public Exception? Exception => services.Select(s => s.Exception).FirstOrDefault();
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

    public Task Stopped { get; }

    [MemberNotNullWhen(true, nameof(Exception))]
    public bool Crashed { get; }

    public Exception? Exception { get; }
}

internal sealed class BackgroundServices : IConcurrentHostedService, IDisposable
{
    public readonly IReadOnlyCollection<BackgroundServiceRunner> Runners;

    public BackgroundServices(IEnumerable<IBackgroundService> services, IHostApplicationLifetime appLifetime)
    {
        Runners = services.Select(s => new BackgroundServiceRunner(s, appLifetime)).ToArray();
    }

    public Task StartAsync(CancellationToken cancellationToken) =>
        Task.WhenAll(Runners.Select(s => s.StartAsync(cancellationToken)));

    public Task StopAsync(CancellationToken cancellationToken) =>
        Task.WhenAll(Runners.Select(s => s.StopAsync(cancellationToken)));

    public void Dispose()
    {
        foreach (var service in Runners)
            service.Dispose();
    }
}

internal sealed class BackgroundServiceRunner(IBackgroundService service, IHostApplicationLifetime appLifetime)
    : IConcurrentHostedService, IBackgroundServiceMonitor, IDisposable
{
    private Task? _start;
    private CancellationTokenSource? _executionCts;
    private Task? _execution;
    private Task? _executionWrapper;

    private readonly TaskCompletionSource<bool> _stopped = new();

    public IBackgroundService Service => service;

    public bool Starting => _start is not null && !_start.IsCompleted;

    // StartedSuccessfully?..
    public bool Started => _start is not null && _start.Status == TaskStatus.RanToCompletion;

    public bool Running => _execution is not null && !_execution.IsCompleted;

    public Task Stopped => _stopped.Task;

    public bool StartCrashed => _start is not null && _start.Status == TaskStatus.Faulted;
    public bool RunCrashed => _execution is not null && _execution.Status == TaskStatus.Faulted;
    public bool Crashed => StartCrashed || RunCrashed;

    // TODO Test
    public Exception? Exception => (StartCrashed ? _start?.Exception : _execution?.Exception)?.InnerException;

    private async Task WaitAppStartAsync(CancellationToken ct)
    {
        try
        {
            // Wait until all other services have started
            await Task.Delay(Timeout.Infinite, appLifetime.ApplicationStarted).WaitAsync(ct);
        }
        catch (OperationCanceledException e) when (e.CancellationToken == appLifetime.ApplicationStarted)
        {
            // Startup completed, continue
        }
    }

    public async Task StartAsync(CancellationToken ct)
    {
        // All the services are started from the same (main) thread, so there are no races
        if (_start is not null)
            throw new InvalidOperationException("Service is already started");

        await (_start = service.StartAsync(ct));

        // Start execution in the background...
        _executionCts = new CancellationTokenSource();
        _executionWrapper = ExecuteAsync(_executionCts.Token);
    }

    private async Task ExecuteAsync(CancellationToken ct)
    {
        try
        {
            await WaitAppStartAsync(ct);
            await (_execution = service.ExecuteAsync(ct));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
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

        try
        {
            if (!_executionCts.IsCancellationRequested)
                _executionCts.Cancel(); // Signal cancellation to the service

            if (_executionWrapper is not null)
                // Wait until the execution completes or the app is forced to exit
                await _executionWrapper.WaitAsync(forceExitToken);

            await service.StopAsync(forceExitToken);
        }
        finally
        {
            _stopped.TrySetResult(true);
        }
    }

    public void Dispose()
    {
        _executionCts?.Dispose();
    }
}

// internal sealed class BackgroundServiceRunner<T>(T service, IHostApplicationLifetime appLifetime)
//     : IServiceFor, IConcurrentHostedService, IBackgroundServiceMonitor, IDisposable
//     where T : class, IBackgroundService
// {
//     private Task? _start;
//     private CancellationTokenSource? _executionCts;
//     private Task? _execution;
//     private Task? _executionWrapper;
//
//     public string Target => service switch
//     {
//         INamedService namedService => namedService.Name,
//         IServiceFor serviceForNamed => serviceForNamed.Target,
//         _ => Reflection.FriendlyNameOf<T>()
//     };
//
//     public bool Starting => _start is not null && !_start.IsCompleted;
//
//     // StartedSuccessfully?..
//     public bool Started => _start is not null && _start.Status == TaskStatus.RanToCompletion;
//
//     public bool Running => _execution is not null && !_execution.IsCompleted;
//
//     public bool StartCrashed => _start is not null && _start.Status == TaskStatus.Faulted;
//     public bool RunCrashed => _execution is not null && _execution.Status == TaskStatus.Faulted;
//     public bool Crashed => StartCrashed || RunCrashed;
//
//     // TODO Test
//     public Exception? Exception => (StartCrashed ? _start?.Exception : _execution?.Exception)?.InnerException;
//
//     private async Task WaitAppStartAsync(CancellationToken ct)
//     {
//         try
//         {
//             // Wait until all other services have started
//             await Task.Delay(Timeout.Infinite, appLifetime.ApplicationStarted).WaitAsync(ct);
//         }
//         catch (OperationCanceledException e) when (e.CancellationToken == appLifetime.ApplicationStarted)
//         {
//             // Startup completed, continue
//         }
//     }
//
//     public async Task StartAsync(CancellationToken ct)
//     {
//         // All the services are started from the same (main) thread, so there are no races
//         if (_start is not null)
//             throw new InvalidOperationException("Service is already started");
//
//         await (_start = service.StartAsync(ct));
//
//         // Start execution in the background...
//         _executionCts = new CancellationTokenSource();
//         _executionWrapper = ExecuteAsync(_executionCts.Token);
//     }
//
//     private async Task ExecuteAsync(CancellationToken ct)
//     {
//         try
//         {
//             await WaitAppStartAsync(ct);
//             await (_execution = service.ExecuteAsync(ct));
//         }
//         catch (OperationCanceledException) when (ct.IsCancellationRequested)
//         {
//             // Normal case, we trigger this token ourselves when stopping the service
//         }
//         catch (Exception)
//         {
//             // Otherwise it's an error, but swallow it silently (this method is called in "fire and forget" mode, not
//             // awaited, so any unhandled exception will arrive in TaskScheduler.UnobservedTaskException, which is not
//             // what we want).
//             // See also: https://stackoverflow.com/a/59300076/322079.
//         }
//     }
//
//     public async Task StopAsync(CancellationToken forceExitToken)
//     {
//         if (_executionCts is null)
//             // Or simply ignore and return?..
//             throw new InvalidOperationException("Service has not been started");
//
//         if (!_executionCts.IsCancellationRequested)
//             _executionCts.Cancel(); // Signal cancellation to the service
//
//         if (_executionWrapper is not null)
//             // Wait until the execution completes or the app is forced to exit
//             await _executionWrapper.WaitAsync(forceExitToken);
//
//         await service.StopAsync(forceExitToken);
//     }
//
//     public void Dispose()
//     {
//         _executionCts?.Dispose();
//     }
// }

internal interface IConcurrentHostedService : IHostedService;

internal sealed class ConcurrentHostedServices(IEnumerable<IConcurrentHostedService> services) : IHostedService
{
    private readonly ImmutableArray<IConcurrentHostedService> _services = services.ToImmutableArray();

    public Task StartAsync(CancellationToken cancellationToken) =>
        Task.WhenAll(_services.Select(c => c.StartAsync(cancellationToken)));

    public Task StopAsync(CancellationToken cancellationToken) =>
        Task.WhenAll(_services.Select(c => c.StopAsync(cancellationToken)));
}
