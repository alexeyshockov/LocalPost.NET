using System.Diagnostics;
using JetBrains.Annotations;

namespace LocalPost.BackgroundQueue;

[PublicAPI]
public static class HandlerStackEx
{
    public static HandlerFactory<ConsumeContext<T>> UsePayload<T>(this HandlerFactory<T> hf) =>
        hf.Map<ConsumeContext<T>, T>(next => async (context, ct) => await next(context.Payload, ct));

    public static HandlerFactory<ConsumeContext<T>> Trace<T>(this HandlerFactory<ConsumeContext<T>> hf)
    {
        var typeName = Reflection.FriendlyNameOf<T>();
        var transactionName = $"{typeName} process";
        return hf.Map<ConsumeContext<T>, ConsumeContext<T>>(next => async (context, ct) =>
        {
            using var activity = context.ActivityContext.HasValue
                ? Tracing.Source.StartActivity(transactionName, ActivityKind.Consumer,
                    context.ActivityContext.Value)
                : Tracing.Source.StartActivity(transactionName, ActivityKind.Consumer);
            try
            {
                await next(context, ct);
                activity?.Success();
            }
            catch (Exception e)
            {
                activity?.Error(e);
                throw;
            }
        });
    }
}
