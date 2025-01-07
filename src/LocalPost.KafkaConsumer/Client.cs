using System.Collections;
using Confluent.Kafka;

namespace LocalPost.KafkaConsumer;

internal sealed class ClientFactory(ILogger logger, ConsumerOptions settings)
{
    public async Task<Clients> Create(CancellationToken ct)
    {
        return new Clients(await Task.WhenAll(Enumerable
            .Range(0, settings.Consumers)
            .Select(_ => Task.Run(CreateClient, ct))
        ).ConfigureAwait(false));

        Client CreateClient()
        {
            var consumer = new ConsumerBuilder<Ignore, byte[]>(settings.ClientConfig)
                .SetErrorHandler((_, e) => logger.LogError("{Error}", e))
                .SetLogHandler((_, m) => logger.LogDebug(m.Message))
                .Build();
            consumer.Subscribe(settings.Topics);
            return new Client(logger, consumer, settings.ClientConfig);
        }
    }
}

internal sealed class Clients(Client[] clients) : IReadOnlyCollection<Client>
{
    public Task Close(CancellationToken ct) => Task.WhenAll(clients.Select(client => Task.Run(client.Close, ct)));

    public IEnumerator<Client> GetEnumerator() => ((IEnumerable<Client>)clients).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => clients.GetEnumerator();

    public int Count => clients.Length;
}

internal sealed class Client
{
    private readonly ILogger _logger;

    public Client(ILogger logger, IConsumer<Ignore, byte[]> consumer, ConsumerConfig config)
    {
        _logger = logger;
        Consumer = consumer;
        Config = config;
        var server = config.BootstrapServers.Split(',')[0].Split(':');
        ServerAddress = server[0];
        if (server.Length > 1)
            ServerPort = int.Parse(server[1]);
    }

    public ConsumeResult<Ignore, byte[]> Consume(CancellationToken ct)
    {
        while (true)
        {
            try
            {
                var result = Consumer.Consume(ct);

                if (result is not null && result.IsPartitionEOF)
                    _logger.LogInformation("End of {Partition} on {Topic}",
                        result.TopicPartition.Partition, result.TopicPartition.Topic);
                else if (result?.Message is not null)
                    return result;
                else
                    _logger.LogWarning("Kafka consumer empty receive");
            }
            // catch (ConsumeException e)
            catch (KafkaException e) when (!e.Error.IsFatal)
            {
                _logger.LogCritical(e, "Kafka consumer (retryable) error: {Reason}", e.Error.Reason);
                // Just continue receiving

                // "generally, the producer should recover from all errors, except where marked fatal" as per
                // https://github.com/confluentinc/confluent-kafka-dotnet/issues/1213#issuecomment-599772818, so
                // just continue polling
            }
        }
    }

    public void Close()
    {
        try
        {
            Consumer.Close();
        }
        finally
        {
            Consumer.Dispose();
        }
    }

    public IConsumer<Ignore, byte[]> Consumer { get; }
    public ConsumerConfig Config { get; }
    public string ServerAddress { get; }
    public int ServerPort { get; } = 9092;
}
