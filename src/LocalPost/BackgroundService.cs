namespace LocalPost;

internal interface IBackgroundService
{
    Task StartAsync(CancellationToken ct);

    Task ExecuteAsync(CancellationToken ct);

    Task StopAsync(CancellationToken ct);
}
