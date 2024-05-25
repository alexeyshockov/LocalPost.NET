using JetBrains.Annotations;

namespace LocalPost.BackgroundQueues;

// Just a proxy to the actual queue, needed to expose IBackgroundJobQueue
[UsedImplicitly]
internal sealed class BackgroundJobQueue(BackgroundQueue<BackgroundJob, ConsumeContext<BackgroundJob>> queue)
    : IBackgroundJobQueue
{
    public ValueTask Enqueue(BackgroundJob payload, CancellationToken ct = default) => queue.Enqueue(payload, ct);
}
