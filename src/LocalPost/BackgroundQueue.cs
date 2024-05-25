namespace LocalPost;

/// <summary>
///     Entrypoint for the background queue, inject it where you need to enqueue items.
/// </summary>
/// <typeparam name="T"></typeparam>
public interface IBackgroundQueue<in T>
{
    // TODO Custom exception when closed?.. Or just return true/false?..
    ValueTask Enqueue(T payload, CancellationToken ct = default);
}



public delegate Task BackgroundJob(CancellationToken ct);

/// <summary>
///     Just a convenient alias for <see cref="IBackgroundQueue{T}" />.
/// </summary>
public interface IBackgroundJobQueue : IBackgroundQueue<BackgroundJob>;
