using System.Runtime.CompilerServices;
using LocalPost.AsyncEnumerable;
using LocalPost.DependencyInjection;

namespace LocalPost.SqsConsumer;

internal sealed class MessageSource : MessageSourceBase, IAsyncEnumerable<ConsumeContext<string>>
{
    private readonly ConcurrentBuffer<ConsumeContext<string>> _source;

    public MessageSource(QueueClient client, int prefetch) : base(client)
    {
        _source = ConsumeAsync().ToConcurrentBuffer(prefetch);
    }

    public override async Task ExecuteAsync(CancellationToken ct) => await _source.Run(ct);

    public IAsyncEnumerator<ConsumeContext<string>> GetAsyncEnumerator(CancellationToken ct) =>
        _source.GetAsyncEnumerator(ct);
}

internal sealed class BatchMessageSource : MessageSourceBase, IAsyncEnumerable<BatchConsumeContext<string>>
{
    private readonly ConcurrentBuffer<BatchConsumeContext<string>> _source;

    // TODO Make a note that Prefetch does not play a role here, with batch processing...
    public BatchMessageSource(QueueClient client,
        BatchBuilderFactory<ConsumeContext<string>, BatchConsumeContext<string>> factory) : base(client)
    {
        _source = ConsumeAsync().Batch(factory).ToConcurrentBuffer();
    }

    public override async Task ExecuteAsync(CancellationToken ct) => await _source.Run(ct);

    public IAsyncEnumerator<BatchConsumeContext<string>> GetAsyncEnumerator(CancellationToken ct) =>
        _source.GetAsyncEnumerator(ct);
}

internal abstract class MessageSourceBase(QueueClient client) : IBackgroundService, INamedService
{
    private bool _stopped;

    public string Name => client.Name;

    public async Task StartAsync(CancellationToken ct) => await client.ConnectAsync(ct);

    public abstract Task ExecuteAsync(CancellationToken ct);

    protected async IAsyncEnumerable<ConsumeContext<string>> ConsumeAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        while (!ct.IsCancellationRequested && !_stopped)
            foreach (var message in await client.PullMessagesAsync(ct))
                yield return new ConsumeContext<string>(client, message, message.Body);

        ct.ThrowIfCancellationRequested();
    }

    public Task StopAsync(CancellationToken ct)
    {
        _stopped = true;

        return Task.CompletedTask;
    }
}
