using LocalPost.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LocalPost;

public sealed partial class BackgroundQueue<T>
{
    // TODO Use
    internal sealed class Supervisor : IHostedService, INamedService
    {
        // TODO Health checks

        public Supervisor(IBackgroundQueueManager<T> queue, string name)
        {
            Queue = queue;
            Name = name;
        }

        internal IBackgroundQueueManager<T> Queue { get; }

        public string Name { get; }

        public Task StartAsync(CancellationToken ct) => Task.CompletedTask;

        public async Task StopAsync(CancellationToken forceExitToken)
        {
            await Queue.CompleteAsync(forceExitToken);
        }
    }
}
