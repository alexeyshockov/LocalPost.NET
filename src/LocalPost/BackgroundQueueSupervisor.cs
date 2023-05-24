using Microsoft.Extensions.Hosting;

namespace LocalPost;

internal sealed partial class BackgroundQueue<T>
{
    internal sealed class Supervisor : IHostedService
    {
        // Health checks later?.. Like full or not.

        private readonly IBackgroundQueueManager _queue;

        public Supervisor(IBackgroundQueueManager queue)
        {
            _queue = queue;
        }

        public Task StartAsync(CancellationToken ct) => Task.CompletedTask;

        public async Task StopAsync(CancellationToken forceExitToken) => await _queue.CompleteAsync(forceExitToken);
    }
}
