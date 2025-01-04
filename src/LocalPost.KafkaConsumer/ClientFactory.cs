using Confluent.Kafka;

namespace LocalPost.KafkaConsumer;

internal sealed class Client
{
    private readonly ILogger<Client> _logger;

    public Client(ILogger<Client> logger, IConsumer<Ignore, byte[]> consumer, ConsumerConfig config)
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

    public IConsumer<Ignore, byte[]> Consumer { get; }
    public ConsumerConfig Config { get; }
    public string ServerAddress { get; }
    public int ServerPort { get; } = 9092;
}

internal sealed class ClientFactory(ILogger<ClientFactory> logger, ILogger<Client> clientLogger) : IDisposable
{
    private List<Client> _clients = [];

    public Client Create(ConsumerConfig config, IEnumerable<string> topics)
    {
        var consumer = new ConsumerBuilder<Ignore, byte[]>(config)
            .SetErrorHandler((_, e) => clientLogger.LogError("{Error}", e))
            .SetLogHandler((_, m) => clientLogger.LogDebug(m.Message))
            .Build();
        consumer.Subscribe(topics);
        var client = new Client(clientLogger, consumer, config);
        _clients.Add(client);
        return client;
    }

    private void Close(IConsumer<Ignore, byte[]> consumer)
    {
        try
        {
            consumer.Close();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error closing Kafka consumer");
        }
        finally
        {
            consumer.Dispose();
        }
    }

    public void Dispose()
    {
        // TODO Run in parallel?..
        foreach (var client in _clients)
            Close(client.Consumer);
        _clients = [];
    }
}
