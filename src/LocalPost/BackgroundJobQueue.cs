using JetBrains.Annotations;

namespace LocalPost;

public delegate Task Job(CancellationToken ct);

/// <summary>
///     Just a convenient alias for <see cref="IBackgroundQueue{T}" />.
/// </summary>
public interface IBackgroundJobQueue : IBackgroundQueue<Job>
{
}

[UsedImplicitly]
internal sealed class BackgroundJobQueue : IBackgroundJobQueue
{
    private readonly BackgroundQueue<Job, Job> _queue;

    public BackgroundJobQueue(BackgroundQueue<Job, Job> queue)
    {
        _queue = queue;
    }

    public ValueTask Enqueue(Job item, CancellationToken ct = default) => _queue.Enqueue(item, ct);
}
