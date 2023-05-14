using System.Threading.Channels;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;

namespace LocalPost.KafkaConsumer;

public static partial class MessageSource<TKey, TValue>
{
    internal sealed class Service : IBackgroundService, IDisposable
    {
        private readonly ILogger<Service> _logger;
        private readonly Options _options;

        private readonly IConsumer<TKey, TValue> _kafka;
        private readonly ChannelWriter<ConsumeContext<TKey, TValue>> _queue;

        public Service(ILogger<Service> logger, string name, Options options, IConsumer<TKey, TValue> kafka,
            ChannelWriter<ConsumeContext<TKey, TValue>> queue)
        {
            _logger = logger;
            _options = options;
            _kafka = kafka;
            _queue = queue;
            Name = name;
        }

        public string Name { get; }

        public Task StartAsync(CancellationToken ct) => Task.Run(() => _kafka.Subscribe(_options.TopicName), ct);

        public Task ExecuteAsync(CancellationToken ct) => Task.Run(() => Run(ct), ct);

        private async Task Run(CancellationToken stoppingToken = default)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Yield();

                try
                {
                    await _queue.WaitToWriteAsync(stoppingToken); // Wait for the buffer capacity

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

                await _queue.WriteAsync(new ConsumeContext<TKey, TValue>
                {
                    Client = _kafka,
                    Result = consumeResult,
                }, stoppingToken);
            }
            catch (ConsumeException e)
            {
                _logger.LogError(e, "Kafka {Topic} consumer error, help link: {HelpLink}",
                    _options.TopicName, e.HelpLink);

                // Bubble up, so the supervisor can report the error and the whole app can be restarted (Kubernetes)
                throw;
            }
        }

        public Task StopAsync(CancellationToken ct) => Task.Run(() =>
        {
            _logger.LogInformation("Stopping Kafka {Topic} consumer...", _options.TopicName);

            _kafka.Close();
            _queue.Complete();
        }, ct);

        public void Dispose()
        {
            _kafka.Dispose();
        }
    }
}
