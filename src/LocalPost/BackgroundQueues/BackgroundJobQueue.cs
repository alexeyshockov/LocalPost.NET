using JetBrains.Annotations;

namespace LocalPost.BackgroundQueues;

// Just a proxy to the actual queue, needed to expose IBackgroundJobQueue
[UsedImplicitly]
internal sealed class BackgroundJobQueue(BackgroundQueue<BackgroundJob, ConsumeContext<BackgroundJob>> queue)
    : IBackgroundJobQueue
{
    public ValueTask Enqueue(ConsumeContext<BackgroundJob> item, CancellationToken ct = default) =>
        queue.Enqueue(item, ct);

    public ValueTask Enqueue(BackgroundJob payload, CancellationToken ct = default) =>
        Enqueue(new ConsumeContext<BackgroundJob>(payload), ct);
}
