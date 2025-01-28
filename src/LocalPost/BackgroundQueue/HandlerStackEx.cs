using System.Diagnostics;

namespace LocalPost.BackgroundQueue;

[PublicAPI]
public static class HandlerStackEx
{
    public static HandlerManagerFactory<ConsumeContext<T>> UseMessagePayload<T>(this HandlerManagerFactory<T> hmf) =>
        hmf.MapHandler<ConsumeContext<T>, T>(next => async (context, ct) =>
            await next(context.Payload, ct).ConfigureAwait(false));

    public static HandlerManagerFactory<ConsumeContext<T>> Trace<T>(this HandlerManagerFactory<ConsumeContext<T>> hmf)
    {
        var typeName = Reflection.FriendlyNameOf<T>();
        var transactionName = $"{typeName} process";
        return hmf.TouchHandler(next => async (context, ct) =>
        {
            using var activity = context.ActivityContext.HasValue
                ? Tracing.Source.StartActivity(transactionName, ActivityKind.Consumer,
                    context.ActivityContext.Value)
                : Tracing.Source.StartActivity(transactionName, ActivityKind.Consumer);
            try
            {
                await next(context, ct).ConfigureAwait(false);
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
