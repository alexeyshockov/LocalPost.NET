namespace LocalPost;

// TODO Remove?..
public interface IQueuePublisher<in T>
{
    // TODO Custom exception when closed?.. Or just return true/false?..
    ValueTask Enqueue(T item, CancellationToken ct = default);
}
