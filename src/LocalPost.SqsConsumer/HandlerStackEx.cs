using System.Text.Json;

namespace LocalPost.SqsConsumer;

[PublicAPI]
public static class HandlerStackEx
{
    public static HandlerFactory<ConsumeContext<T>> UseSqsPayload<T>(this HandlerFactory<T> hf) =>
        hf.Map<ConsumeContext<T>, T>(next => async (context, ct) =>
            await next(context.Payload, ct).ConfigureAwait(false));

    public static HandlerFactory<IEnumerable<ConsumeContext<T>>> UseSqsPayload<T>(
        this HandlerFactory<IEnumerable<T>> hf) =>
        hf.Map<IEnumerable<ConsumeContext<T>>, IEnumerable<T>>(next => async (batch, ct) =>
            await next(batch.Select(context => context.Payload), ct).ConfigureAwait(false));

    public static HandlerFactory<ConsumeContext<T>> Trace<T>(this HandlerFactory<ConsumeContext<T>> hf) =>
        hf.Touch(next => async (context, ct) =>
        {
            using var activity = Tracing.StartProcessing(context);
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

    public static HandlerFactory<ConsumeContext<T>> Acknowledge<T>(this HandlerFactory<ConsumeContext<T>> hf) =>
        hf.Touch(next => async (context, ct) =>
        {
            await next(context, ct).ConfigureAwait(false);
            await context.Client.DeleteMessage(context, ct).ConfigureAwait(false);
        });

    public static HandlerFactory<ConsumeContext<string>> Deserialize<T>(
        this HandlerFactory<ConsumeContext<T>> hf, Func<ConsumeContext<string>, T> deserialize) =>
        hf.Map<ConsumeContext<string>, ConsumeContext<T>>(next =>
            async (context, ct) => await next(context.Transform(deserialize), ct).ConfigureAwait(false));

    public static HandlerFactory<ConsumeContext<string>> DeserializeJson<T>(
        this HandlerFactory<ConsumeContext<T>> hf, JsonSerializerOptions? options = null) =>
        hf.Deserialize(context => JsonSerializer.Deserialize<T>(context.Payload, options)!);
}
