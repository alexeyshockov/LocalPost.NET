using System.Text.Json;
using Confluent.Kafka;
using JetBrains.Annotations;

namespace LocalPost.KafkaConsumer;

// [PublicAPI]
// public static class PipelineOps
// {
//     public static PipelineRegistration<T> Batch<T>(this PipelineRegistration<IEnumerable<T>> next,
//         ushort batchMaxSize = 10, int timeWindowDuration = 1_000) => next.Map<T, IEnumerable<T>>((stream, _) =>
//         stream.Batch(() => new BoundedBatchBuilder<T>(batchMaxSize, timeWindowDuration)));
// }

[PublicAPI]
public static class HandlerStackEx
{
    public static HandlerFactory<ConsumeContext<T>> UseKafkaPayload<T>(this HandlerFactory<T> hf) =>
        hf.Map<ConsumeContext<T>, T>(next => async (context, ct) => await next(context.Payload, ct));

    public static HandlerFactory<IEnumerable<ConsumeContext<T>>> UseKafkaPayload<T>(
        this HandlerFactory<IEnumerable<T>> hf) =>
        hf.Map<IEnumerable<ConsumeContext<T>>, IEnumerable<T>>(next =>
            async (batch, ct) => await next(batch.Select(context => context.Payload), ct));

    public static HandlerFactory<ConsumeContext<T>> Trace<T>(this HandlerFactory<ConsumeContext<T>> hf) =>
        hf.Map<ConsumeContext<T>, ConsumeContext<T>>(next =>
            async (context, ct) =>
            {
                using var activity = Tracing.StartProcessing(context);
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

    public static HandlerFactory<IEnumerable<ConsumeContext<T>>> Trace<T>(this HandlerFactory<IEnumerable<ConsumeContext<T>>> hf) =>
        hf.Map<IEnumerable<ConsumeContext<T>>, IEnumerable<ConsumeContext<T>>>(next =>
            async (batch, ct) =>
            {
                var context = batch.ToList(); // TODO Optimize
                using var activity = Tracing.StartProcessing(context);
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
                context.Client.StoreOffset(context.NextOffset);
            });

    public static HandlerFactory<IEnumerable<ConsumeContext<T>>> Acknowledge<T>(
        this HandlerFactory<IEnumerable<ConsumeContext<T>>> hf) =>
        hf.Map<IEnumerable<ConsumeContext<T>>, IEnumerable<ConsumeContext<T>>>(next =>
            async (batch, ct) =>
            {
                var context = batch.ToList(); // TODO Optimize
                await next(context, ct);
                // Store all the offsets, as it can be a batch of messages from different partitions
                // (even different topics, if subscribed using a regex)
                foreach (var message in context)
                    message.Client.StoreOffset(message.NextOffset);
            });

    #region Deserialize()
    
    public static HandlerFactory<ConsumeContext<byte[]>> Deserialize<T>(
        this HandlerFactory<ConsumeContext<T>> hf, Func<ConsumeContext<byte[]>, T> deserialize) =>
        hf.Map<ConsumeContext<byte[]>, ConsumeContext<T>>(next =>
            async (context, ct) => await next(context.Transform(deserialize), ct));

    public static HandlerFactory<IEnumerable<ConsumeContext<byte[]>>> Deserialize<T>(
        this HandlerFactory<IEnumerable<ConsumeContext<T>>> hf, Func<ConsumeContext<byte[]>, T> deserialize) =>
        hf.Map<IEnumerable<ConsumeContext<byte[]>>, IEnumerable<ConsumeContext<T>>>(next =>
            async (batch, ct) => await next(batch.Select(context => context.Transform(deserialize)), ct));

    public static HandlerFactory<ConsumeContext<byte[]>> Deserialize<T>(
        this HandlerFactory<ConsumeContext<T>> hf, Func<ConsumeContext<byte[]>, Task<T>> deserialize) =>
        hf.Map<ConsumeContext<byte[]>, ConsumeContext<T>>(next =>
            async (context, ct) => await next(await context.Transform(deserialize), ct));

    public static HandlerFactory<IEnumerable<ConsumeContext<byte[]>>> Deserialize<T>(
        this HandlerFactory<IEnumerable<ConsumeContext<T>>> hf, Func<ConsumeContext<byte[]>, Task<T>> deserialize) =>
        hf.Map<IEnumerable<ConsumeContext<byte[]>>, IEnumerable<ConsumeContext<T>>>(next =>
            async (batch, ct) =>
                await next(await Task.WhenAll(batch.Select(context => context.Transform(deserialize))), ct));

    private static Func<ConsumeContext<byte[]>, Task<T>> AsyncDeserializer<T>(IAsyncDeserializer<T> deserializer) =>
        context => deserializer.DeserializeAsync(context.Payload, false, new SerializationContext(
            MessageComponentType.Value, context.Topic, context.Message.Headers));

    public static HandlerFactory<ConsumeContext<byte[]>> Deserialize<T>(
        this HandlerFactory<ConsumeContext<T>> hf, IAsyncDeserializer<T> deserializer) =>
        hf.Deserialize(AsyncDeserializer(deserializer));

    public static HandlerFactory<IEnumerable<ConsumeContext<byte[]>>> Deserialize<T>(
        this HandlerFactory<IEnumerable<ConsumeContext<T>>> hf, IAsyncDeserializer<T> deserializer) =>
        hf.Deserialize(AsyncDeserializer(deserializer));

    private static Func<ConsumeContext<byte[]>, T> Deserializer<T>(IDeserializer<T> deserializer) =>
        context => deserializer.Deserialize(context.Payload, false, new SerializationContext(
            MessageComponentType.Value, context.Topic, context.Message.Headers));

    public static HandlerFactory<ConsumeContext<byte[]>> Deserialize<T>(
        this HandlerFactory<ConsumeContext<T>> hf, IDeserializer<T> deserializer) =>
        hf.Deserialize(Deserializer(deserializer));

    public static HandlerFactory<IEnumerable<ConsumeContext<byte[]>>> Deserialize<T>(
        this HandlerFactory<IEnumerable<ConsumeContext<T>>> hf, IDeserializer<T> deserializer) =>
        hf.Deserialize(Deserializer(deserializer));

    #endregion

    public static HandlerFactory<ConsumeContext<byte[]>> DeserializeJson<T>(
        this HandlerFactory<ConsumeContext<T>> hf, JsonSerializerOptions? options = null) =>
        hf.Deserialize(context => JsonSerializer.Deserialize<T>(context.Payload, options)!);

    public static HandlerFactory<IEnumerable<ConsumeContext<byte[]>>> DeserializeJson<T>(
        this HandlerFactory<IEnumerable<ConsumeContext<T>>> hf, JsonSerializerOptions? options = null) =>
        hf.Deserialize(context => JsonSerializer.Deserialize<T>(context.Payload, options)!);
}
