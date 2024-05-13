using System.Runtime.CompilerServices;
using LocalPost.AsyncEnumerable;
using LocalPost.DependencyInjection;

namespace LocalPost.KafkaConsumer;

internal sealed class MessageSource : MessageSourceBase, IAsyncEnumerable<ConsumeContext<byte[]>>
{
    private readonly ConcurrentAsyncEnumerable<ConsumeContext<byte[]>> _source;

    public MessageSource(KafkaTopicClient client) : base(client)
    {
        _source = ConsumeAsync().ToConcurrent();
    }

    public override async Task ExecuteAsync(CancellationToken ct) => await _source.Run(ct);

    public IAsyncEnumerator<ConsumeContext<byte[]>> GetAsyncEnumerator(CancellationToken ct) =>
        _source.GetAsyncEnumerator(ct);
}

internal sealed class BatchMessageSource : MessageSourceBase, IAsyncEnumerable<BatchConsumeContext<byte[]>>
{
    private readonly ConcurrentAsyncEnumerable<BatchConsumeContext<byte[]>> _source;

    public BatchMessageSource(KafkaTopicClient client,
        BatchBuilderFactory<ConsumeContext<byte[]>, BatchConsumeContext<byte[]>> factory) : base(client)
    {
        _source = ConsumeAsync().Batch(factory).ToConcurrent();
    }

    public override async Task ExecuteAsync(CancellationToken ct) => await _source.Run(ct);

    public IAsyncEnumerator<BatchConsumeContext<byte[]>> GetAsyncEnumerator(CancellationToken ct) =>
        _source.GetAsyncEnumerator(ct);
}

internal abstract class MessageSourceBase : IBackgroundService, INamedService
{
    private readonly KafkaTopicClient _client;

    private bool _stopped;

    // Some additional reading: https://devblogs.microsoft.com/premier-developer/the-danger-of-taskcompletionsourcet-class/
//    private readonly TaskCompletionSource<bool> _executionTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    protected MessageSourceBase(KafkaTopicClient client)
    {
        _client = client;
    }

    public string Name => _client.Name;

    // Run on a separate thread, as Confluent Kafka API is blocking
    public Task StartAsync(CancellationToken ct) => Task.Run(() => _client.Subscribe(), ct);

    public abstract Task ExecuteAsync(CancellationToken ct);

    protected async IAsyncEnumerable<ConsumeContext<byte[]>> ConsumeAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        // TODO Transaction activity...

        // Give the control back in the beginning, just before blocking in the Kafka's consumer call
        await Task.Yield();
        foreach (var result in Consume(ct))
            yield return result;
    }

    private IEnumerable<ConsumeContext<byte[]>> Consume(CancellationToken ct)
    {
        // TODO Transaction activity...

        while (!ct.IsCancellationRequested && !_stopped)
            yield return _client.Read(ct);

        ct.ThrowIfCancellationRequested();
    }

    // Run on a separate thread, as Confluent Kafka API is blocking
    public Task StopAsync(CancellationToken ct) => Task.Run(() =>
    {
        _stopped = true;
        _client.Close();
    }, ct);
}
