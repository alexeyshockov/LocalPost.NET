using System.Collections.Immutable;
using Microsoft.Extensions.DependencyInjection;

namespace LocalPost.SqsConsumer;

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
//     private readonly ImmutableDictionary<string, QueueClient> _clients;
//
//     public AcknowledgeMiddleware(IEnumerable<QueueClient> clients)
//     {
//         _clients = clients.ToImmutableDictionary(client => client.Name, client => client);
//     }
//
//     public Handler<ConsumeContext<T>> Invoke<T>(Handler<ConsumeContext<T>> next) => async (context, ct) =>
//     {
//         await next(context, ct);
//         await _clients[context.ClientName].DeleteMessageAsync(context);
//     };
//
//     public Handler<BatchConsumeContext<T>> Invoke<T>(Handler<BatchConsumeContext<T>> next) => async (context, ct) =>
//     {
//         await next(context, ct);
//         await _clients[context.ClientName].DeleteMessagesAsync(context);
//     };
// }
