using System.Collections.Immutable;
using System.Text.Json;
using JetBrains.Annotations;

namespace LocalPost.SqsConsumer;

[PublicAPI]
public static class HandlerStackEx
{
    public static HandlerFactory<ConsumeContext<T>> UseSqsPayload<T>(this HandlerFactory<T> hf) =>
        hf.Map<ConsumeContext<T>, T>(next => async (context, ct) => await next(context.Payload, ct));

    public static HandlerFactory<BatchConsumeContext<T>> UseSqsPayload<T>(this HandlerFactory<IReadOnlyList<T>> hf) =>
        hf.Map<BatchConsumeContext<T>, IReadOnlyList<T>>(next =>
            async (context, ct) => await next(context.Messages.Select(m => m.Payload).ToImmutableList(), ct));

    public static HandlerFactory<ConsumeContext<T>> Trace<T>(
        this HandlerFactory<ConsumeContext<T>> handlerStack) =>
        handlerStack.Map<ConsumeContext<T>, ConsumeContext<T>>(next => async (context, ct) =>
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

    public static HandlerFactory<BatchConsumeContext<T>> Trace<T>(
        this HandlerFactory<BatchConsumeContext<T>> handlerStack) =>
        handlerStack.Map<BatchConsumeContext<T>, BatchConsumeContext<T>>(next => async (context, ct) =>
        {
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

    public static HandlerFactory<ConsumeContext<T>> Acknowledge<T>(
        this HandlerFactory<ConsumeContext<T>> handlerStack) =>
        // handlerStack.Map(Middlewares.Acknowledge<T>);
        handlerStack.Map<ConsumeContext<T>, ConsumeContext<T>>(next =>
            async (context, ct) =>
            {
                await next(context, ct);
                await context.Client.DeleteMessageAsync(context); // TODO Instrument
            });

    public static HandlerFactory<BatchConsumeContext<T>> Acknowledge<T>(
        this HandlerFactory<BatchConsumeContext<T>> handlerStack) =>
        // handlerStack.Map(Middlewares.AcknowledgeBatch<T>);
        handlerStack.Map<BatchConsumeContext<T>, BatchConsumeContext<T>>(next =>
            async (context, ct) =>
            {
                await next(context, ct);
                await context.Client.DeleteMessagesAsync(context); // TODO Instrument
            });

    public static HandlerFactory<ConsumeContext<string>> Deserialize<T>(
        this HandlerFactory<ConsumeContext<T>> handlerStack, Func<ConsumeContext<string>, T> deserialize) =>
        handlerStack.Map<ConsumeContext<string>, ConsumeContext<T>>(next =>
            async (context, ct) => await next(context.Transform(deserialize), ct));

    public static HandlerFactory<BatchConsumeContext<string>> Deserialize<T>(
        this HandlerFactory<BatchConsumeContext<T>> handlerStack, Func<ConsumeContext<string>, T> deserialize) =>
        handlerStack.Map<BatchConsumeContext<string>, BatchConsumeContext<T>>(next =>
            async (context, ct) => await next(context.Transform(deserialize), ct));

    public static HandlerFactory<ConsumeContext<string>> Deserialize<T>(
        this HandlerFactory<ConsumeContext<T>> handlerStack, Func<ConsumeContext<string>, Task<T>> deserialize) =>
        handlerStack.Map<ConsumeContext<string>, ConsumeContext<T>>(next =>
            async (context, ct) => await next(await context.Transform(deserialize), ct));

    public static HandlerFactory<BatchConsumeContext<string>> Deserialize<T>(
        this HandlerFactory<BatchConsumeContext<T>> handlerStack, Func<ConsumeContext<string>, Task<T>> deserialize) =>
        handlerStack.Map<BatchConsumeContext<string>, BatchConsumeContext<T>>(next =>
            async (context, ct) => await next(await context.Transform(deserialize), ct));

    public static HandlerFactory<ConsumeContext<string>> DeserializeJson<T>(
        this HandlerFactory<ConsumeContext<T>> hf, JsonSerializerOptions? options = null) =>
        hf.Deserialize(context => JsonSerializer.Deserialize<T>(context.Payload, options)!);

    public static HandlerFactory<BatchConsumeContext<string>> DeserializeJson<T>(
        this HandlerFactory<BatchConsumeContext<T>> hf, JsonSerializerOptions? options = null) =>
        hf.Deserialize(context => JsonSerializer.Deserialize<T>(context.Payload, options)!);
}
