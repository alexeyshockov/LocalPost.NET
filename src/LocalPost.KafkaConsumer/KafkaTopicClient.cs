using Confluent.Kafka;
using LocalPost.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LocalPost.KafkaConsumer;

internal static class KafkaLogging
{
    public static void LogKafkaMessage(this ILogger logger, string topic, LogMessage log)
    {
        var level = log.Level switch
        {
            SyslogLevel.Emergency => LogLevel.Critical,
            SyslogLevel.Alert => LogLevel.Critical,
            SyslogLevel.Critical => LogLevel.Critical,
            SyslogLevel.Error => LogLevel.Error,
            SyslogLevel.Warning => LogLevel.Warning,
            SyslogLevel.Notice => LogLevel.Information,
            SyslogLevel.Info => LogLevel.Information,
            SyslogLevel.Debug => LogLevel.Debug,
            _ => LogLevel.Information
        };

        logger.Log(level, "{Topic} (via librdkafka): {Message}", topic, log.Message);
    }
}

internal sealed class KafkaTopicClient : INamedService, IDisposable
{
    private readonly ILogger<KafkaTopicClient> _logger;
    private readonly IConsumer<Ignore, byte[]> _client;

    public KafkaTopicClient(ILogger<KafkaTopicClient> logger, ConsumerConfig config, string topic, string name)
    {
        _logger = logger;

        _client = new ConsumerBuilder<Ignore, byte[]>(config)
            .SetLogHandler((_, log) => _logger.LogKafkaMessage(topic, log))
            .Build();

        Topic = topic;
        GroupId = config.GroupId;
        Name = name;
    }

    public string Topic { get; }

    public string GroupId { get; }

    public string Name { get; }

    public void Subscribe() => _client.Subscribe(Topic);

    public void Close()
    {
        _logger.LogInformation("Stopping Kafka {Topic} consumer...", Topic);

        _client.Close(); // No need for additional .Dispose() call
    }

    public void StoreOffset(TopicPartitionOffset topicPartitionOffset) =>
        _client.StoreOffset(topicPartitionOffset);

    public ConsumeContext<byte[]> Read(CancellationToken ct = default)
    {
        while (true)
        {
            try
            {
                var result = _client.Consume(ct);

                // Log an empty receive?..
                if (result is null || result.IsPartitionEOF || result.Message is null)
                    continue; // Continue waiting for a message

                return new ConsumeContext<byte[]>(this,
                    new TopicPartitionOffset(result.Topic, result.Partition, result.Offset + 1, result.LeaderEpoch),
                    result.Message,
                    result.Message.Value);
            }
            catch (ConsumeException e) when (!e.Error.IsFatal)
            {
                _logger.LogError(e, "Kafka {Topic} consumer error, more details: {HelpLink}",
                    Topic, e.HelpLink);

                // "generally, the producer should recover from all errors, except where marked fatal" as per
                // https://github.com/confluentinc/confluent-kafka-dotnet/issues/1213#issuecomment-599772818, so
                // just continue polling
            }
        }
    }

    public void Dispose()
    {
        _client.Dispose();
    }
}
