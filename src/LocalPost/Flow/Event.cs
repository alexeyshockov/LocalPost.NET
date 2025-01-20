namespace LocalPost.Flow;




public interface IHandlerManager<in T>
{
    ValueTask Start(CancellationToken ct);

    ValueTask Handle(T payload, CancellationToken ct); // Handler<T>

    ValueTask Stop(Exception? error, CancellationToken ct);
}

internal class HandlerManager<T>(Handler<T> handler) : IHandlerManager<T>
{
    public ValueTask Start(CancellationToken ct) => ValueTask.CompletedTask;

    public ValueTask Handle(T payload, CancellationToken ct) => handler(payload, ct);

    public ValueTask Stop(Exception? error, CancellationToken ct) => ValueTask.CompletedTask;
}


// public enum EventType : byte
// {
//     Message, // With required payload
//     Begin, // Empty
//     End, // With an optional error
// }
//
// [PublicAPI]
// public readonly record struct Event<T>(EventType Type, T Payload = default!, Exception? Error = null)
// {
//     public static Event<T> Begin => new(EventType.Begin);
//     public static Event<T> End => new(EventType.End);
//     public static Event<T> Fail(Exception e) => new(EventType.End, Error: e);
//
//     public static implicit operator Event<T>(T payload) => new(EventType.Message, payload);
// }
