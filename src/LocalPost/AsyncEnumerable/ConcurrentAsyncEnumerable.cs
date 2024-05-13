using System.Threading.Channels;

namespace LocalPost.AsyncEnumerable;

internal sealed class ConcurrentAsyncEnumerable<T> : IAsyncEnumerable<T>
{
    private readonly IAsyncEnumerable<T> _reader;
    private readonly Channel<T> _buffer;

    public ConcurrentAsyncEnumerable(IAsyncEnumerable<T> source, int bufferMaxSize = 1)
    {
        _reader = source;
        _buffer = Channel.CreateBounded<T>(new BoundedChannelOptions(bufferMaxSize)
        {
            SingleReader = false,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.Wait,
        });
    }

    public async Task Run(CancellationToken ct)
    {
        var buffer = _buffer.Writer;
        try
        {
            await foreach (var item in _reader.WithCancellation(ct))
                await buffer.WriteAsync(item, ct);
        }
        finally
        {
            buffer.Complete();
        }
    }

    public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken ct = default)
    {
        var buffer = _buffer.Reader;
        // Like ReadAllAsync() from netstandard2.1
        while (await buffer.WaitToReadAsync(ct).ConfigureAwait(false))
            while (buffer.TryRead(out var item))
                yield return item;
    }
}
