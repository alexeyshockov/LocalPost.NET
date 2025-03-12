using System.Threading.Channels;

namespace LocalPost.BackgroundQueue;

// Just a proxy to the actual queue, needed to expose IBackgroundJobQueue
[UsedImplicitly]
internal sealed class BackgroundJobQueue(IBackgroundQueue<BackgroundJob> queue) : IBackgroundJobQueue
{
    public ValueTask Enqueue(BackgroundJob payload, CancellationToken ct = default) => queue.Enqueue(payload, ct);

    public ChannelWriter<ConsumeContext<BackgroundJob>> Writer => queue.Writer;
}
