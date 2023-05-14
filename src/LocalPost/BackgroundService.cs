using LocalPost.DependencyInjection;

namespace LocalPost;

internal interface IBackgroundService : INamedService
{
    Task StartAsync(CancellationToken ct);

    Task ExecuteAsync(CancellationToken ct);

    Task StopAsync(CancellationToken ct);
}
