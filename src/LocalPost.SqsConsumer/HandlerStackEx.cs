using System.Text.Json;

namespace LocalPost.SqsConsumer;

using MessageHmf = HandlerManagerFactory<ConsumeContext<string>>;
using MessagesHmf = HandlerManagerFactory<IReadOnlyCollection<ConsumeContext<string>>>;

[PublicAPI]
public static class HandlerStackEx
{
    public static HandlerManagerFactory<ConsumeContext<T>> UseSqsPayload<T>(this HandlerManagerFactory<T> hmf) =>
        hmf.MapHandler<ConsumeContext<T>, T>(next => async (context, ct) =>
            await next(context.Payload, ct).ConfigureAwait(false));

    public static HandlerManagerFactory<IReadOnlyCollection<ConsumeContext<T>>> UseSqsPayload<T>(
        this HandlerManagerFactory<IReadOnlyCollection<T>> hmf) =>
        hmf.MapHandler<IReadOnlyCollection<ConsumeContext<T>>, IReadOnlyCollection<T>>(next => async (batch, ct) =>
            await next(batch.Select(context => context.Payload).ToArray(), ct).ConfigureAwait(false));

    public static HandlerManagerFactory<ConsumeContext<T>> Trace<T>(this HandlerManagerFactory<ConsumeContext<T>> hmf) =>
        hmf.TouchHandler(next => async (context, ct) =>
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

    public static HandlerManagerFactory<IReadOnlyCollection<ConsumeContext<T>>> Trace<T>(
        this HandlerManagerFactory<IReadOnlyCollection<ConsumeContext<T>>> hmf) =>
        hmf.TouchHandler(next => async (batch, ct) =>
        {
            using var activity = Tracing.StartProcessing(batch);
            try
            {
                await next(batch, ct).ConfigureAwait(false);
                activity?.Success();
            }
            catch (Exception e)
            {
                activity?.Error(e);
                throw;
            }
        });

    public static HandlerManagerFactory<ConsumeContext<T>> Acknowledge<T>(
        this HandlerManagerFactory<ConsumeContext<T>> hmf) => hmf.TouchHandler(next =>
        async (context, ct) =>
        {
            await next(context, ct).ConfigureAwait(false);
            // await context.Client.DeleteMessage(context, ct).ConfigureAwait(false);
            await context.DeleteMessage(ct).ConfigureAwait(false);
        });

    public static HandlerManagerFactory<IReadOnlyCollection<ConsumeContext<T>>> Acknowledge<T>(
        this HandlerManagerFactory<IReadOnlyCollection<ConsumeContext<T>>> hmf) => hmf.TouchHandler(next =>
        async (batch, ct) =>
        {
            await next(batch, ct).ConfigureAwait(false);
            if (batch.Count > 0)
                await batch.First().Client.DeleteMessages(batch, ct).ConfigureAwait(false);
        });

    public static MessageHmf Deserialize<T>(
        this HandlerManagerFactory<ConsumeContext<T>> hmf,
        Func<IServiceProvider, Func<ConsumeContext<string>, T>> df) => provider =>
    {
        var handler = hmf(provider);
        var deserialize = df(provider);
        return handler.Map<ConsumeContext<string>, ConsumeContext<T>>(next =>
            async (context, ct) => await next(context.Transform(deserialize), ct).ConfigureAwait(false));
    };

    public static MessagesHmf Deserialize<T>(
        this HandlerManagerFactory<IReadOnlyCollection<ConsumeContext<T>>> hmf,
        Func<IServiceProvider, Func<ConsumeContext<string>, T>> df) => provider =>
    {
        var handler = hmf(provider);
        var deserialize = df(provider);
        return handler.Map<IReadOnlyCollection<ConsumeContext<string>>, IReadOnlyCollection<ConsumeContext<T>>>(next =>
            async (batch, ct) =>
            {
                var modBatch = batch.Select(context => context.Transform(deserialize)).ToArray();
                await next(modBatch, ct).ConfigureAwait(false);
            });
    };

    public static MessageHmf DeserializeJson<T>(
        this HandlerManagerFactory<ConsumeContext<T>> hmf,
        JsonSerializerOptions? options = null) =>
        hmf.Deserialize(_ => context => JsonSerializer.Deserialize<T>(context.Payload, options)!);

    public static MessagesHmf DeserializeJson<T>(
        this HandlerManagerFactory<IReadOnlyCollection<ConsumeContext<T>>> hmf,
        JsonSerializerOptions? options = null) =>
        hmf.Deserialize(_ => context => JsonSerializer.Deserialize<T>(context.Payload, options)!);
}
