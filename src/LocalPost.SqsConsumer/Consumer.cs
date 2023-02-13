using System.Threading.Channels;
using Microsoft.Extensions.Options;

namespace LocalPost.SqsConsumer;

public static partial class Consumer
{
    internal sealed class Service : IBackgroundService
    {
        private readonly QueueClient _client;
        private readonly Channel<ConsumeContext> _queue;

        public Service(string name, QueueClient client, IOptionsMonitor<ConsumerOptions> options)
        {
            var config = options.Get(name);

            _client = client;
            _queue = Channel.CreateBounded<ConsumeContext>(new BoundedChannelOptions(config.BufferSize)
            {
                SingleWriter = true,
                SingleReader = true
                // TODO AllowSynchronousContinuations?..
            });

            Name = name;
        }

        public string Name { get; }

        public IAsyncEnumerable<ConsumeContext> Messages => _queue.Reader.ReadAllAsync();

        public async Task StartAsync(CancellationToken ct)
        {
            await _client.ConnectAsync(ct);
        }

        public async Task ExecuteAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                await _queue.Writer.WaitToWriteAsync(ct); // Wait for the buffer capacity

                await Consume(ct);
            }
        }

        public Task StopAsync(CancellationToken ct) => Task.CompletedTask;

        private async Task Consume(CancellationToken stoppingToken)
        {
            var messages = await _client.PullMessagesAsync(stoppingToken);

            foreach (var message in messages)
                await _queue.Writer.WriteAsync(message, stoppingToken);
        }
    }
}
