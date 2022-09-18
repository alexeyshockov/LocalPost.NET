using System.Threading.Channels;

namespace LocalPost.AmazonSns;

internal static partial class ChannelExtensions
{
    public static ChannelReader<TOut> Batch<T, TOut>(this ChannelReader<T> reader, BatchBuilderFactory<T, TOut> batchFactory) =>
        new BatchingChannelReader<T, TOut>(reader, batchFactory);
}

internal sealed class BatchingChannelReader<T, TOut> : ChannelReader<TOut>
{
    private readonly ChannelReader<T> _reader;
    private readonly Channel<TOut> _buffer;
    private readonly BatchBuilderFactory<T, TOut> _factory;

    public BatchingChannelReader(ChannelReader<T> source, BatchBuilderFactory<T, TOut> factory)
    {
        _reader = source;
        _buffer = Channel.CreateUnbounded<TOut>(new UnboundedChannelOptions // Bounded?..
        {
            SingleWriter = true,
            SingleReader = false
        });
        _factory = factory;
    }

    public override Task Completion => _buffer.Reader.Completion;
    public override bool CanCount => _buffer.Reader.CanCount;
    public override int Count => _buffer.Reader.Count;

    private async ValueTask<bool> WaitToReadAsyncCore(ValueTask<bool> bufferWait, CancellationToken userCancellation)
    {
        var batch = _factory();
        do
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(userCancellation, batch.TimeWindow);
                var itemWait = _reader.WaitToReadAsync(cts.Token);
                var completed = !await itemWait;
                if (completed)
                    break;

                while (_reader.TryRead(out var item))
                    if (!batch.Add(item))
                    {
                        await _buffer.Writer.WriteAsync(batch.BuildAndDispose(), userCancellation);
                        batch = _factory();
                        batch.Add(item);
                    }
            }
            catch (OperationCanceledException) when (batch.TimeWindow.IsCancellationRequested)
            {
                if (!batch.IsEmpty)
                    await _buffer.Writer.WriteAsync(batch.BuildAndDispose(), userCancellation);

                batch = _factory();
            }
        } while (!bufferWait.IsCompleted);

        if (!_reader.Completion.IsCompleted) return true;

        // Flush on completion...
        if (!batch.IsEmpty)
            await _buffer.Writer.WriteAsync(batch.BuildAndDispose(), userCancellation);

        _buffer.Writer.Complete();

        return false;
    }

    public override ValueTask<bool> WaitToReadAsync(CancellationToken cancellationToken = default)
    {
        if (_buffer.Reader.Completion.IsCompleted)
            return new ValueTask<bool>(false);

        if (cancellationToken.IsCancellationRequested)
            return new ValueTask<bool>(Task.FromCanceled<bool>(cancellationToken));

        var wait = _buffer.Reader.WaitToReadAsync(cancellationToken);
        return wait.IsCompleted ? wait : WaitToReadAsyncCore(wait, cancellationToken);
    }

    public override bool TryRead(out TOut item) => _buffer.Reader.TryRead(out item!);
}
