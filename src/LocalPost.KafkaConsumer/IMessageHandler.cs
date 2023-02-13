using Confluent.Kafka;

namespace LocalPost.KafkaConsumer;

public interface IMessageHandler<TKey, TValue> : LocalPost.IHandler<Message<TKey, TValue>>
{
}
