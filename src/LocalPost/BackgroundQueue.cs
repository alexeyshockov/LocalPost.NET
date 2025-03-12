using System.Threading.Channels;
using LocalPost.BackgroundQueue;

namespace LocalPost;

/// <summary>
///     Background queue publisher.
/// </summary>
/// <typeparam name="T">Payload type.</typeparam>
public interface IBackgroundQueue<T>
{
    ValueTask Enqueue(T payload, CancellationToken ct = default);

    ChannelWriter<ConsumeContext<T>> Writer { get; }
}

public delegate Task BackgroundJob(CancellationToken ct);

/// <summary>
///     Just a convenient alias for <see cref="IBackgroundQueue{BackgroundJob}" /> queue.
/// </summary>
public interface IBackgroundJobQueue : IBackgroundQueue<BackgroundJob>;
