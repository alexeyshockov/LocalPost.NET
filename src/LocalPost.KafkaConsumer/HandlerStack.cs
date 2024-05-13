using Confluent.Kafka;
using JetBrains.Annotations;

namespace LocalPost.KafkaConsumer;

[PublicAPI]
public static class KafkaHandlerStack
{
    public static HandlerFactory<ConsumeContext<T>> Trace<T>(
        this HandlerFactory<ConsumeContext<T>> handlerStack) =>
        handlerStack.Map<ConsumeContext<T>, ConsumeContext<T>>(next =>
            async (context, ct) =>
            {
                using var activity = KafkaActivitySource.StartProcessing(context);
                try
                {
                    await next(context, ct);
                    activity?.Success();
                }
                catch (Exception ex)
                {
                    activity?.Error(ex);
                }
            });

    public static HandlerFactory<BatchConsumeContext<T>> Trace<T>(
        this HandlerFactory<BatchConsumeContext<T>> handlerStack) =>
        handlerStack.Map<BatchConsumeContext<T>, BatchConsumeContext<T>>(next =>
            async (context, ct) =>
            {
                using var activity = KafkaActivitySource.StartProcessing(context);
                try
                {
                    await next(context, ct);
                    activity?.Success();
                }
                catch (Exception ex)
                {
                    activity?.Error(ex);
                }
            });

    public static HandlerFactory<ConsumeContext<T>> Acknowledge<T>(
        this HandlerFactory<ConsumeContext<T>> handlerStack) =>
        handlerStack.Map<ConsumeContext<T>, ConsumeContext<T>>(next =>
            async (context, ct) =>
            {
                await next(context, ct);
                context.Client.StoreOffset(context.Offset);
            });

    public static HandlerFactory<BatchConsumeContext<T>> Acknowledge<T>(
        this HandlerFactory<BatchConsumeContext<T>> handlerStack) =>
        handlerStack.Map<BatchConsumeContext<T>, BatchConsumeContext<T>>(next =>
            async (context, ct) =>
            {
                await next(context, ct);
                context.Client.StoreOffset(context.LatestOffset);
            });

    public static HandlerFactory<ConsumeContext<byte[]>> Deserialize<T>(
        this HandlerFactory<ConsumeContext<T>> handlerStack, IAsyncDeserializer<T> deserializer)
    {
        var middleware = new DeserializationMiddleware<T> { Deserializer = deserializer };

        return handlerStack.Map(middleware.Invoke);
    }

    public static HandlerFactory<BatchConsumeContext<byte[]>> Deserialize<T>(
        this HandlerFactory<BatchConsumeContext<T>> handlerStack, IAsyncDeserializer<T> deserializer)
    {
        var middleware = new DeserializationMiddleware<T> { Deserializer = deserializer };

        return handlerStack.Map(middleware.Invoke);
    }
}
