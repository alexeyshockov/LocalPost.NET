using Confluent.Kafka;

namespace LocalPost.KafkaConsumer;

public interface IMessageHandler<TKey, TValue> : IHandler<Message<TKey, TValue>>
{
}

public interface IMessageHandler<TValue> : IMessageHandler<Ignore, TValue>
{
}
