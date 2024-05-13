using Confluent.Kafka;

namespace LocalPost.KafkaConsumer;

internal class OffsetManager
{
    // partition -> next offset
    private readonly Dictionary<TopicPartition, NextOffset> _offsets = new();

    public void Register(TopicPartitionOffset topicPartitionOffset)
    {
        lock (_offsets)
        {
            if (!_offsets.ContainsKey(topicPartitionOffset.TopicPartition))
                _offsets[topicPartitionOffset.TopicPartition] = NextOffset.From(topicPartitionOffset);
        }
    }

    public async ValueTask WaitToStore(TopicPartitionOffset topicPartitionOffset)
    {
        var offset = topicPartitionOffset.Offset;
        var topicPartition = topicPartitionOffset.TopicPartition;
//        if (!_offsets.ContainsKey(topicPartition))
//            throw new ArgumentOutOfRangeException(nameof(topicPartitionOffset), "Unknown topic partition");

        var completed = false;
        while (!completed)
        {
            NextOffset nextOffset;
            lock (_offsets)
            {
                nextOffset = _offsets[topicPartition];
                completed = nextOffset.Offset >= offset;
                if (completed)
                    _offsets[topicPartition] = nextOffset.Next();
            }

            if (completed)
                nextOffset.Complete();
            else
                await nextOffset.Completed;
        }
    }
}

internal readonly record struct NextOffset(Offset Offset)
{
    // See https://devblogs.microsoft.com/premier-developer/the-danger-of-taskcompletionsourcet-class/#conclusion
    private readonly TaskCompletionSource<bool> _completionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task Completed => _completionSource.Task;

    public NextOffset Next() => new(Offset + 1);

    public void Complete() => _completionSource.SetResult(true);

    public static NextOffset From(TopicPartitionOffset topicPartitionOffset) => new(topicPartitionOffset.Offset);
}
