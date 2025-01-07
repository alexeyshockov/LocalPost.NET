using System.Text.Json;
using Confluent.Kafka;

namespace LocalPost.KafkaConsumer;

[PublicAPI]
public static class HandlerStackEx
{
    public static HandlerFactory<ConsumeContext<T>> UseKafkaPayload<T>(this HandlerFactory<T> hf) =>
        hf.Map<ConsumeContext<T>, T>(next => async (context, ct) =>
            await next(context.Payload, ct).ConfigureAwait(false));

    public static HandlerFactory<ConsumeContext<T>> Trace<T>(this HandlerFactory<ConsumeContext<T>> hf) =>
        hf.Map<ConsumeContext<T>, ConsumeContext<T>>(next =>
            async (context, ct) =>
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
        hf.Map<ConsumeContext<T>, ConsumeContext<T>>(next =>
            async (context, ct) =>
            {
                await next(context, ct).ConfigureAwait(false);
                context.Acknowledge();
            });

    #region Deserialize()

    public static HandlerFactory<ConsumeContext<byte[]>> Deserialize<T>(
        this HandlerFactory<ConsumeContext<T>> hf, Func<ConsumeContext<byte[]>, T> deserialize) =>
        hf.Map<ConsumeContext<byte[]>, ConsumeContext<T>>(next => async (context, ct) =>
            await next(context.Transform(deserialize), ct).ConfigureAwait(false));

    public static HandlerFactory<ConsumeContext<byte[]>> Deserialize<T>(
        this HandlerFactory<ConsumeContext<T>> hf, Func<ConsumeContext<byte[]>, Task<T>> deserialize) =>
        hf.Map<ConsumeContext<byte[]>, ConsumeContext<T>>(next => async (context, ct) =>
            await next(await context.Transform(deserialize).ConfigureAwait(false), ct).ConfigureAwait(false));

    private static Func<ConsumeContext<byte[]>, Task<T>> AsyncDeserializer<T>(IAsyncDeserializer<T> deserializer) =>
        context => deserializer.DeserializeAsync(context.Payload, false, new SerializationContext(
            MessageComponentType.Value, context.Topic, context.Message.Headers));

    public static HandlerFactory<ConsumeContext<byte[]>> Deserialize<T>(
        this HandlerFactory<ConsumeContext<T>> hf, IAsyncDeserializer<T> deserializer) =>
        hf.Deserialize(AsyncDeserializer(deserializer));

    private static Func<ConsumeContext<byte[]>, T> Deserializer<T>(IDeserializer<T> deserializer) =>
        context => deserializer.Deserialize(context.Payload, false, new SerializationContext(
            MessageComponentType.Value, context.Topic, context.Message.Headers));

    public static HandlerFactory<ConsumeContext<byte[]>> Deserialize<T>(
        this HandlerFactory<ConsumeContext<T>> hf, IDeserializer<T> deserializer) =>
        hf.Deserialize(Deserializer(deserializer));

    #endregion

    public static HandlerFactory<ConsumeContext<byte[]>> DeserializeJson<T>(
        this HandlerFactory<ConsumeContext<T>> hf, JsonSerializerOptions? options = null) =>
        hf.Deserialize(context => JsonSerializer.Deserialize<T>(context.Payload, options)!);
}
