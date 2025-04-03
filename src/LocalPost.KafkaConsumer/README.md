# LocalPost Kafka Consumer

[![NuGet package](https://img.shields.io/nuget/dt/LocalPost.KafkaConsumer)](https://www.nuget.org/packages/LocalPost.KafkaConsumer/)

## librdkafka's background prefetching

The Kafka client automatically prefetches messages in the background. This is done by the background thread that is
started when the client is created. The background thread will fetch messages from the broker and enqueue them on the
internal queue, so `Consume()` calls will return faster.

Because of this behavior, there is no need to maintain our own in memory queue (channel).

## Concurrent processing

A Kafka consumer is designed to handle messages _from one partition_ sequentially, as it commits the offset of the last
processed message.

One of the common ways to speed up things (increase throughput) is to have multiple partitions for a topic and multiple
parallel consumers.

Another way is to batch process messages.

## Message key ignorance

Kafka's message key is used for almost one and only one purpose: to determine the partition for the message, when
publishing. And in almost all the cases this information is also available (serialized) in the message itself
(message value in Kafka terms). That's why we are ignoring the message key in this consumer.
