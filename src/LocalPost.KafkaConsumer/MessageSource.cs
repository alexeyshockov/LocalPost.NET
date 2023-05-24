using System.Threading.Channels;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;

namespace LocalPost.KafkaConsumer;

internal sealed class MessageSource<TKey, TValue> : IBackgroundService,
    IAsyncEnumerable<ConsumeContext<TKey, TValue>>, IDisposable
{
    private readonly ILogger<MessageSource<TKey, TValue>> _logger;
    private readonly string _topicName;

    private readonly IConsumer<TKey, TValue> _kafka;
    private readonly Channel<ConsumeContext<TKey, TValue>> _queue;

    public MessageSource(ILogger<MessageSource<TKey, TValue>> logger, string topicName, IConsumer<TKey, TValue> kafka)
    {
        _logger = logger;
        _topicName = topicName;
        _kafka = kafka;
        _queue = Channel.CreateBounded<ConsumeContext<TKey, TValue>>(new BoundedChannelOptions(1)
        {
            SingleWriter = true,
            SingleReader = false
        });
    }

    public async IAsyncEnumerator<ConsumeContext<TKey, TValue>> GetAsyncEnumerator(CancellationToken ct = default)
    {
        // Track full or not later
        while (await _queue.Reader.WaitToReadAsync(ct))
            while (_queue.Reader.TryRead(out var item))
                yield return item;
    }

    public static implicit operator ChannelReader<ConsumeContext<TKey, TValue>>(MessageSource<TKey, TValue> that) =>
        that._queue.Reader;

    public static implicit operator ChannelWriter<ConsumeContext<TKey, TValue>>(MessageSource<TKey, TValue> that) =>
        that._queue.Writer;

    public Task StartAsync(CancellationToken ct) => Task.Run(() => _kafka.Subscribe(_topicName), ct);

    public Task ExecuteAsync(CancellationToken ct) => Task.Run(() => Run(ct), ct);

    private async Task Run(CancellationToken stoppingToken = default)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Yield();

            try
            {
                await _queue.Writer.WaitToWriteAsync(stoppingToken); // Wait for the buffer capacity

                await Consume(stoppingToken);
            }
            catch (OperationCanceledException e) when (e.CancellationToken == stoppingToken)
            {
                // Just complete the method normally...
            }
        }
    }

    private async Task Consume(CancellationToken stoppingToken)
    {
        // TODO Transaction activity...
        try
        {
            var consumeResult = _kafka.Consume(stoppingToken);

            if (consumeResult is null || consumeResult.IsPartitionEOF || consumeResult.Message is null)
                return; // Continue the loop

            await _queue.Writer.WriteAsync(new ConsumeContext<TKey, TValue>
            {
//                    Client = _kafka,
                Result = consumeResult,
            }, stoppingToken);
        }
        catch (ConsumeException e)
        {
            _logger.LogError(e, "Kafka {Topic} consumer error, help link: {HelpLink}",
                _topicName, e.HelpLink);

            // Bubble up, so the supervisor can report the error and the whole app can be restarted (by Kubernetes or
            // another orchestrator)
            throw;
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.Run(() =>
    {
        _logger.LogInformation("Stopping Kafka {Topic} consumer...", _topicName);

        _kafka.Close();
        _queue.Writer.Complete();
    }, ct);

    public void Dispose()
    {
        _kafka.Dispose();
    }
}
