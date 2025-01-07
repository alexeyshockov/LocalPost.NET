namespace LocalPost.Flow;

public enum EventType : byte
{
    Message,
    Begin,
    End,
}

[PublicAPI]
public readonly record struct Event<T>(EventType Type, T Payload = default)
{
    public static Event<T> Begin => new(EventType.Begin);
    public static Event<T> End => new(EventType.End);

    public static implicit operator Event<T>(T payload) => new(EventType.Message, payload);
}
