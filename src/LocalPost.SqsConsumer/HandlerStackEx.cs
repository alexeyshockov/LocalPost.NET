using System.Text.Json;
using JetBrains.Annotations;

namespace LocalPost.SqsConsumer;

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
    public static HandlerFactory<ConsumeContext<T>> UseSqsPayload<T>(this HandlerFactory<T> hf) =>
        hf.Map<ConsumeContext<T>, T>(next => async (context, ct) => await next(context.Payload, ct));

    public static HandlerFactory<IEnumerable<ConsumeContext<T>>> UseSqsPayload<T>(
        this HandlerFactory<IEnumerable<T>> hf) =>
        hf.Map<IEnumerable<ConsumeContext<T>>, IEnumerable<T>>(next =>
            async (batch, ct) => await next(batch.Select(context => context.Payload), ct));

    public static HandlerFactory<ConsumeContext<T>> Trace<T>(this HandlerFactory<ConsumeContext<T>> hf) =>
        hf.Touch(next => async (context, ct) =>
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

    public static HandlerFactory<IEnumerable<ConsumeContext<T>>> Trace<T>(
        this HandlerFactory<IEnumerable<ConsumeContext<T>>> hf) =>
        hf.Touch(next => async (batch, ct) =>
        {
            var context = batch.ToList(); // TODO Optimize
            // TODO Link distributed transactions from each message
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
        hf.Touch(next =>
            async (context, ct) =>
            {
                await next(context, ct);
                await context.Client.DeleteMessageAsync(context); // TODO Instrument
            });

    public static HandlerFactory<IEnumerable<ConsumeContext<T>>> Acknowledge<T>(
        this HandlerFactory<IEnumerable<ConsumeContext<T>>> hf) =>
        hf.Touch(next =>
            async (batch, ct) =>
            {
                var context = batch.ToList(); // TODO Optimize
                var client = context.First().Client;
                await next(context, ct);
                await client.DeleteMessagesAsync(context);
            });

    public static HandlerFactory<ConsumeContext<string>> Deserialize<T>(
        this HandlerFactory<ConsumeContext<T>> handlerStack, Func<ConsumeContext<string>, T> deserialize) =>
        handlerStack.Map<ConsumeContext<string>, ConsumeContext<T>>(next =>
            async (context, ct) => await next(context.Transform(deserialize), ct));

    public static HandlerFactory<IEnumerable<ConsumeContext<string>>> Deserialize<T>(
        this HandlerFactory<IEnumerable<ConsumeContext<T>>> hf, Func<ConsumeContext<string>, T> deserialize) =>
        hf.Map<IEnumerable<ConsumeContext<string>>, IEnumerable<ConsumeContext<T>>>(next =>
            async (batch, ct) => await next(batch.Select(context => context.Transform(deserialize)), ct));

    // public static HandlerFactory<ConsumeContext<string>> Deserialize<T>(
    //     this HandlerFactory<ConsumeContext<T>> handlerStack, Func<ConsumeContext<string>, Task<T>> deserialize) =>
    //     handlerStack.Map<ConsumeContext<string>, ConsumeContext<T>>(next =>
    //         async (context, ct) => await next(await context.Transform(deserialize), ct));
    //
    // public static HandlerFactory<IEnumerable<ConsumeContext<string>>> Deserialize<T>(
    //     this HandlerFactory<IEnumerable<ConsumeContext<T>>> hf, Func<ConsumeContext<string>, Task<T>> deserialize) =>
    //     hf.Map<IEnumerable<ConsumeContext<string>>, IEnumerable<ConsumeContext<T>>>(next =>
    //         async (context, ct) => await next(await context.Transform(deserialize), ct));

    public static HandlerFactory<ConsumeContext<string>> DeserializeJson<T>(
        this HandlerFactory<ConsumeContext<T>> hf, JsonSerializerOptions? options = null) =>
        hf.Deserialize(context => JsonSerializer.Deserialize<T>(context.Payload, options)!);

    public static HandlerFactory<IEnumerable<ConsumeContext<string>>> DeserializeJson<T>(
        this HandlerFactory<IEnumerable<ConsumeContext<T>>> hf, JsonSerializerOptions? options = null) =>
        hf.Deserialize(context => JsonSerializer.Deserialize<T>(context.Payload, options)!);
}
