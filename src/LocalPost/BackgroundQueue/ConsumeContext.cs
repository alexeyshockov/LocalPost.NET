using System.Diagnostics;
using JetBrains.Annotations;

namespace LocalPost.BackgroundQueue;

[PublicAPI]
public readonly record struct ConsumeContext<T> // TODO Rename
{
    public readonly ActivityContext? ActivityContext;
    public readonly T Payload;

    internal ConsumeContext(T payload) : this(payload, Activity.Current?.Context)
    {
    }

    internal ConsumeContext(T payload, ActivityContext? activityContext)
    {
        Payload = payload;
        ActivityContext = activityContext;
    }

    public ConsumeContext<TOut> Transform<TOut>(TOut payload) => new(payload, ActivityContext);

    public static implicit operator ConsumeContext<T>(T payload) => new(payload);

    public static implicit operator T(ConsumeContext<T> context) => context.Payload;

    public void Deconstruct(out T payload)
    {
        payload = Payload;
    }
}
