using Confluent.Kafka;

namespace LocalPost.KafkaConsumer;

using MessageHmf = HandlerManagerFactory<ConsumeContext<byte[]>>;
using MessagesHmf = HandlerManagerFactory<IReadOnlyCollection<ConsumeContext<byte[]>>>;

[PublicAPI]
public static class HandlerStackEx
{
    public static HandlerManagerFactory<ConsumeContext<T>> UseKafkaPayload<T>(this HandlerManagerFactory<T> hmf) =>
        hmf.MapHandler<ConsumeContext<T>, T>(next => async (context, ct) =>
            await next(context.Payload, ct).ConfigureAwait(false));

    public static HandlerManagerFactory<IReadOnlyCollection<ConsumeContext<T>>> UseKafkaPayload<T>(
        this HandlerManagerFactory<IReadOnlyCollection<T>> hmf) =>
        hmf.MapHandler<IReadOnlyCollection<ConsumeContext<T>>, IReadOnlyCollection<T>>(next => async (batch, ct) =>
            await next(batch.Select(context => context.Payload).ToArray(), ct).ConfigureAwait(false));

    public static HandlerManagerFactory<ConsumeContext<T>> Trace<T>(
        this HandlerManagerFactory<ConsumeContext<T>> hmf) =>
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

    /// <summary>
    ///     Manually acknowledge every message (store offset).
    ///
    ///     Works only when EnableAutoOffsetStore is false!
    /// </summary>
    /// <param name="hmf">Message handler factory.</param>
    /// <typeparam name="T">Message type.</typeparam>
    /// <returns>Wrapped handler factory.</returns>
    public static HandlerManagerFactory<ConsumeContext<T>> Acknowledge<T>(
        this HandlerManagerFactory<ConsumeContext<T>> hmf) =>
        hmf.TouchHandler(next => async (context, ct) =>
        {
            await next(context, ct).ConfigureAwait(false);
            context.StoreOffset();
        });

    /// <summary>
    ///     Manually acknowledge every message (store offset).
    ///
    ///     Works only when EnableAutoOffsetStore is false!
    /// </summary>
    /// <param name="hmf">Message handler factory.</param>
    /// <typeparam name="T">Message type.</typeparam>
    /// <returns>Wrapped handler factory.</returns>
    public static HandlerManagerFactory<IReadOnlyCollection<ConsumeContext<T>>> Acknowledge<T>(
        this HandlerManagerFactory<IReadOnlyCollection<ConsumeContext<T>>> hmf) =>
        hmf.TouchHandler(next => async (batch, ct) =>
        {
            await next(batch, ct).ConfigureAwait(false);
            foreach (var context in batch)
                context.StoreOffset();
        });

    private static Func<ConsumeContext<byte[]>, Task<T>> AsyncDeserializer<T>(IAsyncDeserializer<T> deserializer) =>
        context => deserializer.DeserializeAsync(context.Payload, false, new SerializationContext(
            MessageComponentType.Value, context.Topic, context.Message.Headers));

    private static Func<ConsumeContext<byte[]>, T> Deserializer<T>(IDeserializer<T> deserializer) =>
        context => deserializer.Deserialize(context.Payload, false, new SerializationContext(
            MessageComponentType.Value, context.Topic, context.Message.Headers));

    #region Deserialize()

    public static MessageHmf Deserialize<T>(this HandlerManagerFactory<ConsumeContext<T>> hmf,
        Func<ConsumeContext<byte[]>, T> deserialize) =>
        hmf.MapHandler<ConsumeContext<byte[]>, ConsumeContext<T>>(next => async (context, ct) =>
            await next(context.Transform(deserialize), ct).ConfigureAwait(false));

    public static MessageHmf Deserialize<T>(this HandlerManagerFactory<ConsumeContext<T>> hmf,
        Func<ConsumeContext<byte[]>, Task<T>> deserialize) =>
        hmf.MapHandler<ConsumeContext<byte[]>, ConsumeContext<T>>(next => async (context, ct) =>
            await next(await context.Transform(deserialize).ConfigureAwait(false), ct).ConfigureAwait(false));

    public static MessageHmf Deserialize<T>(this HandlerManagerFactory<ConsumeContext<T>> hmf,
        IAsyncDeserializer<T> deserializer) => hmf.Deserialize(AsyncDeserializer(deserializer));

    public static MessageHmf Deserialize<T>(this HandlerManagerFactory<ConsumeContext<T>> hmf,
        IDeserializer<T> deserializer) => hmf.Deserialize(Deserializer(deserializer));



    public static MessagesHmf Deserialize<T>(
        this HandlerManagerFactory<IReadOnlyCollection<ConsumeContext<T>>> hmf,
        Func<ConsumeContext<byte[]>, T> deserialize) =>
        hmf.MapHandler<IReadOnlyCollection<ConsumeContext<byte[]>>, IReadOnlyCollection<ConsumeContext<T>>>(next =>
            async (batch, ct) =>
            {
                var modBatch = batch.Select(context => context.Transform(deserialize)).ToArray();
                await next(modBatch, ct).ConfigureAwait(false);
            });

    public static MessagesHmf Deserialize<T>(
        this HandlerManagerFactory<IReadOnlyCollection<ConsumeContext<T>>> hmf,
        Func<ConsumeContext<byte[]>, Task<T>> deserialize) =>
        hmf.MapHandler<IReadOnlyCollection<ConsumeContext<byte[]>>, IReadOnlyCollection<ConsumeContext<T>>>(next =>
            async (batch, ct) =>
            {
                var modifications = batch.Select(context => context.Transform(deserialize));
                // Task.WhenAll() preserves the order
                var modBatch = await Task.WhenAll(modifications).ConfigureAwait(false);
                await next(modBatch, ct).ConfigureAwait(false);
            });

    public static MessagesHmf Deserialize<T>(
        this HandlerManagerFactory<IReadOnlyCollection<ConsumeContext<T>>> hmf,
        IAsyncDeserializer<T> deserializer) => hmf.Deserialize(AsyncDeserializer(deserializer));

    public static MessagesHmf Deserialize<T>(
        this HandlerManagerFactory<IReadOnlyCollection<ConsumeContext<T>>> hmf,
        IDeserializer<T> deserializer) => hmf.Deserialize(Deserializer(deserializer));

    #endregion

    // public static HandlerFactory<ConsumeContext<byte[]>> DeserializeJson<T>(
    //     this HandlerFactory<ConsumeContext<T>> hf, JsonSerializerOptions? options = null) =>
    //     hf.Deserialize(context => JsonSerializer.Deserialize<T>(context.Payload, options)!);
}
