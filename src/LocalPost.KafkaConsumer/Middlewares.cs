using System.Collections.Immutable;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;

namespace LocalPost.KafkaConsumer;

// internal static class Middlewares
// {
//     public static HandlerMiddleware<ConsumeContext<T>, ConsumeContext<T>> Acknowledge<T>(IServiceProvider provider) =>
//         provider.GetRequiredService<AcknowledgeMiddleware>().Invoke<T>;
//
//     public static HandlerMiddleware<BatchConsumeContext<T>, BatchConsumeContext<T>> AcknowledgeBatch<T>(
//         IServiceProvider provider) => provider.GetRequiredService<AcknowledgeMiddleware>().Invoke<T>;
// }
//
// internal sealed class AcknowledgeMiddleware
// {
//     private readonly ImmutableDictionary<string, KafkaTopicClient> _clients;
//
//     public AcknowledgeMiddleware(IEnumerable<KafkaTopicClient> clients)
//     {
//         _clients = clients.ToImmutableDictionary(client => client.Name, client => client);
//     }
//
//     public Handler<ConsumeContext<T>> Invoke<T>(Handler<ConsumeContext<T>> next) => async (context, ct) =>
//     {
//         await next(context, ct);
//         _clients[context.ClientName].StoreOffset(context.Message);
//     };
//
//     public Handler<BatchConsumeContext<T>> Invoke<T>(Handler<BatchConsumeContext<T>> next) => async (context, ct) =>
//     {
//         await next(context, ct);
//         _clients[context.ConsumerName].StoreOffset(context.Latest);
//     };
// }

internal sealed class DeserializationMiddleware<T> :
    IHandlerMiddleware<BatchConsumeContext<byte[]>, BatchConsumeContext<T>>,
    IHandlerMiddleware<ConsumeContext<byte[]>, ConsumeContext<T>>
{
    public required IAsyncDeserializer<T> Deserializer { get; init; }

    public Handler<ConsumeContext<byte[]>> Invoke(Handler<ConsumeContext<T>> next) =>
        async (context, ct) => await next(await Deserialize(context), ct);

    public Handler<BatchConsumeContext<byte[]>> Invoke(Handler<BatchConsumeContext<T>> next) =>
        async (context, ct) =>
        {
            var messages = await Task.WhenAll(context.Messages.Select(Deserialize));
            await next(context.Transform(messages), ct);
        };

    private async Task<ConsumeContext<T>> Deserialize(ConsumeContext<byte[]> context)
    {
        var payload = await Deserializer.DeserializeAsync(context.Payload, false, new SerializationContext(
            MessageComponentType.Value, context.Topic, context.Message.Headers));

        return context.Transform(payload);
    }
}
