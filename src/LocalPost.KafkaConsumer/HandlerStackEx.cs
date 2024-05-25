using System.Collections.Immutable;
using System.Text.Json;
using Confluent.Kafka;
using JetBrains.Annotations;

namespace LocalPost.KafkaConsumer;

[PublicAPI]
public static class HandlerStackEx
{
    public static HandlerFactory<ConsumeContext<T>> UseKafkaPayload<T>(this HandlerFactory<T> hf) =>
        hf.Map<ConsumeContext<T>, T>(next => async (context, ct) => await next(context.Payload, ct));

    public static HandlerFactory<BatchConsumeContext<T>> UseKafkaPayload<T>(this HandlerFactory<IReadOnlyList<T>> hf) =>
        hf.Map<BatchConsumeContext<T>, IReadOnlyList<T>>(next =>
            async (context, ct) => await next(context.Messages.Select(m => m.Payload).ToImmutableList(), ct));

    public static HandlerFactory<ConsumeContext<T>> Trace<T>(this HandlerFactory<ConsumeContext<T>> hf) =>
        hf.Map<ConsumeContext<T>, ConsumeContext<T>>(next =>
            async (context, ct) =>
            {
                using var activity = KafkaActivitySource.StartProcessing(context);
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

    public static HandlerFactory<BatchConsumeContext<T>> Trace<T>(this HandlerFactory<BatchConsumeContext<T>> hf) =>
        hf.Map<BatchConsumeContext<T>, BatchConsumeContext<T>>(next =>
            async (context, ct) =>
            {
                using var activity = KafkaActivitySource.StartProcessing(context);
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

    public static HandlerFactory<ConsumeContext<T>> Acknowledge<T>(this HandlerFactory<ConsumeContext<T>> hf) =>
        hf.Map<ConsumeContext<T>, ConsumeContext<T>>(next =>
            async (context, ct) =>
            {
                await next(context, ct);
                context.Client.StoreOffset(context.Offset);
            });

    public static HandlerFactory<BatchConsumeContext<T>> Acknowledge<T>(
        this HandlerFactory<BatchConsumeContext<T>> hf) =>
        hf.Map<BatchConsumeContext<T>, BatchConsumeContext<T>>(next =>
            async (context, ct) =>
            {
                await next(context, ct);
                context.Client.StoreOffset(context.LatestOffset);
            });

    #region Deserialize()
    
    public static HandlerFactory<ConsumeContext<byte[]>> Deserialize<T>(
        this HandlerFactory<ConsumeContext<T>> hf, Func<ConsumeContext<byte[]>, T> deserialize) =>
        hf.Map<ConsumeContext<byte[]>, ConsumeContext<T>>(next =>
            async (context, ct) => await next(context.Transform(deserialize), ct));

    public static HandlerFactory<BatchConsumeContext<byte[]>> Deserialize<T>(
        this HandlerFactory<BatchConsumeContext<T>> hf, Func<ConsumeContext<byte[]>, T> deserialize) =>
        hf.Map<BatchConsumeContext<byte[]>, BatchConsumeContext<T>>(next =>
            async (context, ct) => await next(context.Transform(deserialize), ct));

    public static HandlerFactory<ConsumeContext<byte[]>> Deserialize<T>(
        this HandlerFactory<ConsumeContext<T>> hf, Func<ConsumeContext<byte[]>, Task<T>> deserialize) =>
        hf.Map<ConsumeContext<byte[]>, ConsumeContext<T>>(next =>
            async (context, ct) => await next(await context.Transform(deserialize), ct));

    public static HandlerFactory<BatchConsumeContext<byte[]>> Deserialize<T>(
        this HandlerFactory<BatchConsumeContext<T>> hf, Func<ConsumeContext<byte[]>, Task<T>> deserialize) =>
        hf.Map<BatchConsumeContext<byte[]>, BatchConsumeContext<T>>(next =>
            async (context, ct) => await next(await context.Transform(deserialize), ct));

    private static Func<ConsumeContext<byte[]>, Task<T>> AsyncDeserializer<T>(IAsyncDeserializer<T> deserializer) =>
        context => deserializer.DeserializeAsync(context.Payload, false, new SerializationContext(
            MessageComponentType.Value, context.Topic, context.Message.Headers));

    public static HandlerFactory<ConsumeContext<byte[]>> Deserialize<T>(
        this HandlerFactory<ConsumeContext<T>> hf, IAsyncDeserializer<T> deserializer) =>
        hf.Deserialize(AsyncDeserializer(deserializer));

    public static HandlerFactory<BatchConsumeContext<byte[]>> Deserialize<T>(
        this HandlerFactory<BatchConsumeContext<T>> hf, IAsyncDeserializer<T> deserializer) =>
        hf.Deserialize(AsyncDeserializer(deserializer));

    private static Func<ConsumeContext<byte[]>, T> Deserializer<T>(IDeserializer<T> deserializer) =>
        context => deserializer.Deserialize(context.Payload, false, new SerializationContext(
            MessageComponentType.Value, context.Topic, context.Message.Headers));

    public static HandlerFactory<ConsumeContext<byte[]>> Deserialize<T>(
        this HandlerFactory<ConsumeContext<T>> hf, IDeserializer<T> deserializer) =>
        hf.Deserialize(Deserializer(deserializer));

    public static HandlerFactory<BatchConsumeContext<byte[]>> Deserialize<T>(
        this HandlerFactory<BatchConsumeContext<T>> hf, IDeserializer<T> deserializer) =>
        hf.Deserialize(Deserializer(deserializer));

    #endregion

    public static HandlerFactory<ConsumeContext<byte[]>> DeserializeJson<T>(
        this HandlerFactory<ConsumeContext<T>> hf, JsonSerializerOptions? options = null) =>
        hf.Deserialize(context => JsonSerializer.Deserialize<T>(context.Payload, options)!);

    public static HandlerFactory<BatchConsumeContext<byte[]>> DeserializeJson<T>(
        this HandlerFactory<BatchConsumeContext<T>> hf, JsonSerializerOptions? options = null) =>
        hf.Deserialize(context => JsonSerializer.Deserialize<T>(context.Payload, options)!);
}
