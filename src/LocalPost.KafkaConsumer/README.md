# LocalPost Kafka Consumer

## librdkafka's background prefetching

The Kafka client automatically prefetches messages in the background. This is done by the background thread that is
started when the client is created. The background thread will fetch messages from the broker and enqueue them on the
internal queue, so `Consume()` calls will return faster.

Because of this behavior, there is no need to maintain our own in memory queue (channel).









## Immutability (actually lack of it)

Because Kafka messages are meant to be processed sequentially (parallelism is achieved by having multiple
partitions / consumers), `ConsumeContext`/`BatchConsumeContext` objects are not immutable and are reused for each
handler's call.
