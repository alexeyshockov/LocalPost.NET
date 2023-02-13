using System.Threading.Channels;
using Confluent.Kafka;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LocalPost.KafkaConsumer;

public static partial class Consumer<TKey, TValue>
{
    internal sealed class Service : BackgroundService
    {
        private readonly ILogger<Service> _logger;
        private readonly ConsumerOptions _options;

        private readonly IConsumer<TKey, TValue> _kafka;
        private readonly ChannelWriter<ConsumeContext<TKey, TValue>> _queue;
        private readonly Handler<ConsumeException> _errorHandler;

        public Service(ILogger<Service> logger, ConsumerOptions options, string name, IConsumer<TKey, TValue> kafka,
            ChannelWriter<ConsumeContext<TKey, TValue>> queue, Handler<ConsumeException> errorHandler)
        {
            _logger = logger;
            _options = options;
            _kafka = kafka;
            _queue = queue;
            _errorHandler = errorHandler;
            Name = name;
        }

        public string Name { get; }

        public bool Closed { get; private set; }

        protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
            Task.Run(() => Run(stoppingToken), stoppingToken);

        private async Task Run(CancellationToken stoppingToken = default)
        {
            _kafka.Subscribe(_options.TopicName);

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

            _logger.LogInformation("Stopping Kafka {Topic} consumer...", _options.TopicName);
            Closed = true;
            _kafka.Close();
            _queue.Complete();
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

                await _errorHandler(e, stoppingToken); // TODO exit the app if configured...
            }
        }

        public override void Dispose()
        {
            base.Dispose();
            _kafka.Dispose();
        }
    }
}
