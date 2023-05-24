using System.Collections.Immutable;
using Microsoft.Extensions.Hosting;

namespace LocalPost;

internal interface IConcurrentHostedService : IHostedService
{
}

internal sealed class HostedServices : IHostedService, IDisposable
{
    private readonly ImmutableArray<IConcurrentHostedService> _services;

    public HostedServices(IEnumerable<IConcurrentHostedService> services)
    {
        _services = services.ToImmutableArray();
    }

    public Task StartAsync(CancellationToken cancellationToken) =>
        Task.WhenAll(_services.Select(c => c.StartAsync(cancellationToken)));

    public Task StopAsync(CancellationToken cancellationToken) =>
        Task.WhenAll(_services.Select(c => c.StopAsync(cancellationToken)));

    public void Dispose()
    {
        foreach (var service in _services)
            if (service is IDisposable disposable)
                disposable.Dispose();
    }
}
