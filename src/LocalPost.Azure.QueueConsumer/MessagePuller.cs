using System.Diagnostics;
using Azure;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Microsoft.Extensions.Options;

namespace LocalPost.Azure.QueueConsumer;

internal sealed class MessagePuller : IAsyncEnumerable<QueueMessage>
{
    private static readonly ActivitySource Tracer = new(typeof(MessagePuller).Namespace);

    private readonly QueueClient _queue;
    private readonly ConsumerOptions _options;

    public MessagePuller(IAzureQueues queues, string name, IOptionsMonitor<ConsumerOptions> options)
    {
        _options = options.Get(name);
        var queueName = _options.QueueName ?? throw new ArgumentNullException(nameof(options), "Queue name is required");

        Name = name;

        _queue = queues.Get(queueName);
    }

    public string Name { get; }

    public async IAsyncEnumerator<QueueMessage> GetAsyncEnumerator(CancellationToken ct = default)
    {
        while (!ct.IsCancellationRequested)
        {
            var messages = await PullMessagesAsync(ct);

            foreach (var message in messages)
                yield return message;
        }
    }

    private async Task<IEnumerable<QueueMessage>> PullMessagesAsync(CancellationToken ct)
    {
        using var span = Tracer.StartActivity();

        // Azure SDK handles network failures
        var response = await _queue.ReceiveMessagesAsync(_options.BufferSize, null, ct);

        return response.Value;
    }

    public Handler<QueueMessage> Wrap(Handler<QueueMessage> handler) => async (message, ct) =>
    {
        await handler(message, ct);

        // Won't be deleted in case of an exception in the handler
        await DeleteMessageAsync(message, ct);
    };

    private async Task DeleteMessageAsync(QueueMessage message, CancellationToken ct)
    {
        using var span = Tracer.StartActivity();

        await _queue.DeleteMessageAsync(message.MessageId, message.PopReceipt, ct);

        // TODO Log failures?..
    }
}
