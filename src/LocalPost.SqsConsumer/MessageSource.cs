using System.Runtime.CompilerServices;
using LocalPost.AsyncEnumerable;
using LocalPost.DependencyInjection;

namespace LocalPost.SqsConsumer;

internal sealed class MessageSource : MessageSourceBase, IAsyncEnumerable<ConsumeContext<string>>
{
    private readonly ConcurrentAsyncEnumerable<ConsumeContext<string>> _source;

    public MessageSource(QueueClient client) : base(client)
    {
        _source = ConsumeAsync().ToConcurrent();
    }

    public override async Task ExecuteAsync(CancellationToken ct) => await _source.Run(ct);

    public IAsyncEnumerator<ConsumeContext<string>> GetAsyncEnumerator(CancellationToken ct) =>
        _source.GetAsyncEnumerator(ct);
}

internal sealed class BatchMessageSource : MessageSourceBase, IAsyncEnumerable<BatchConsumeContext<string>>
{
    private readonly ConcurrentAsyncEnumerable<BatchConsumeContext<string>> _source;

    public BatchMessageSource(QueueClient client,
        BatchBuilderFactory<ConsumeContext<string>, BatchConsumeContext<string>> factory) : base(client)
    {
        _source = ConsumeAsync().Batch(factory).ToConcurrent();
    }

    public override async Task ExecuteAsync(CancellationToken ct) => await _source.Run(ct);

    public IAsyncEnumerator<BatchConsumeContext<string>> GetAsyncEnumerator(CancellationToken ct) =>
        _source.GetAsyncEnumerator(ct);
}

internal abstract class MessageSourceBase : IBackgroundService, INamedService
{
    private readonly QueueClient _client;

    private bool _stopped;

    protected MessageSourceBase(QueueClient client)
    {
        _client = client;
    }

    public string Name => _client.Name;

    public async Task StartAsync(CancellationToken ct) => await _client.ConnectAsync(ct);

    public abstract Task ExecuteAsync(CancellationToken ct);

    protected async IAsyncEnumerable<ConsumeContext<string>> ConsumeAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        while (!ct.IsCancellationRequested && !_stopped)
            foreach (var message in await _client.PullMessagesAsync(ct))
                yield return message;

        ct.ThrowIfCancellationRequested();
    }

    // Run on a separate thread, as Confluent Kafka API is blocking
    public Task StopAsync(CancellationToken ct)
    {
        _stopped = true;
//        _client.Close();

        return Task.CompletedTask;
    }
}
