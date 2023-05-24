using System.Threading.Channels;

namespace LocalPost.SqsConsumer;

internal sealed class MessageSource : IBackgroundService, IAsyncEnumerable<ConsumeContext>
{
    private readonly QueueClient _client;
    private readonly Channel<ConsumeContext> _queue;

    public MessageSource(QueueClient client)
    {
        _client = client;
        _queue = Channel.CreateBounded<ConsumeContext>(new BoundedChannelOptions(1)
        {
            SingleWriter = true, // Spawn multiple readers later?..
            SingleReader = false
        });
    }

    public async IAsyncEnumerator<ConsumeContext> GetAsyncEnumerator(CancellationToken ct = default)
    {
        // Track full or not later
        while (await _queue.Reader.WaitToReadAsync(ct))
            while (_queue.Reader.TryRead(out var item))
                yield return item;
    }

    public static implicit operator ChannelReader<ConsumeContext>(MessageSource that) => that._queue.Reader;

    public static implicit operator ChannelWriter<ConsumeContext>(MessageSource that) => that._queue.Writer;

    public async Task StartAsync(CancellationToken ct)
    {
        await _client.ConnectAsync(ct);
    }

    public async Task ExecuteAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await _queue.Writer.WaitToWriteAsync(ct); // Wait for the buffer capacity

                await Consume(ct);
            }
        }
        finally
        {
        }
    }

    public Task StopAsync(CancellationToken ct)
    {
        _queue.Writer.Complete();

        return Task.CompletedTask;
    }

    private async Task Consume(CancellationToken stoppingToken)
    {
        var messages = await _client.PullMessagesAsync(stoppingToken);

        foreach (var message in messages)
            await _queue.Writer.WriteAsync(message, stoppingToken);
    }
}
