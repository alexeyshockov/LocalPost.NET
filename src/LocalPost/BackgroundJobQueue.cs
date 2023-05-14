namespace LocalPost;

public delegate Task Job(CancellationToken ct);

/// <summary>
///     Just a convenient alias for <see cref="IBackgroundQueue{T}" />.
/// </summary>
public interface IBackgroundJobQueue : IBackgroundQueue<Job>
{
}

internal sealed class BackgroundJobQueue : IBackgroundJobQueue, IBackgroundQueueManager<Job>
{
    private readonly BackgroundQueue<Job> _queue;

    public BackgroundJobQueue(BackgroundQueue<Job> queue)
    {
        _queue = queue;
    }

    public bool IsClosed => _queue.IsClosed;

    public ValueTask Enqueue(Job item, CancellationToken ct = default) => _queue.Enqueue(item, ct);

    public ValueTask CompleteAsync(CancellationToken ct = default) => _queue.CompleteAsync(ct);
}
