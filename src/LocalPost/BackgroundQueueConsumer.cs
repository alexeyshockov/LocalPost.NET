using System.Threading.Channels;
using LocalPost.AsyncEnumerable;

namespace LocalPost;

internal sealed partial class BackgroundQueue<T>
{
    internal sealed class Consumer : IBackgroundService
    {
        private readonly IAsyncEnumerable<T> _reader;
        private readonly Handler<T> _handler;

        public Consumer(IAsyncEnumerable<T> reader, Handler<T> handler)
        {
            _reader = reader;
            _handler = handler;
        }

        public Task StartAsync(CancellationToken ct) => Task.CompletedTask;

        public async Task ExecuteAsync(CancellationToken ct)
        {
            await foreach (var message in _reader.WithCancellation(ct))
                await _handler(message, ct);
        }

        public async Task StopAsync(CancellationToken forceExitToken)
        {
            // Good to have later: an option to NOT process the rest of the messages...
            await foreach (var message in _reader.WithCancellation(forceExitToken))
                await _handler(message, forceExitToken);
        }
    }

    internal sealed class BatchConsumer<TOut> : IBackgroundService
    {
        private readonly ChannelReader<T> _source;
        private readonly ChannelWriter<TOut> _destination;
        private readonly BatchBuilderFactory<T, TOut> _factory;

        private IBatchBuilder<T, TOut> _batch;

        public BatchConsumer(ChannelReader<T> source, ChannelWriter<TOut> destination, BatchBuilderFactory<T, TOut> factory)
        {
            _source = source;
            _destination = destination;
            _factory = factory;

            _batch = factory();
        }

        private async Task ProcessBatch(CancellationToken ct)
        {
            await _destination.WriteAsync(_batch.Build(), ct);
            _batch.Dispose();
            _batch = _factory();
        }

        private async Task Process(CancellationToken ct)
        {
            while (true)
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct, _batch.TimeWindow);
                try
                {
                    if (!await _source.WaitToReadAsync(cts.Token))
                        break;

                    while (_source.TryRead(out var message))
                    {
                        if (_batch.TryAdd(message)) continue;
                        if (_batch.IsEmpty)
                            throw new Exception("Cannot fit a message in a batch");

                        await ProcessBatch(ct);

                        if (!_batch.TryAdd(message))
                            throw new Exception("Cannot fit a message in a batch");
                    }
                }
                catch (OperationCanceledException e) when (e.CancellationToken == _batch.TimeWindow)
                {
                    // Just process the current batch
                    if (!_batch.IsEmpty)
                        await ProcessBatch(ct);
                }
            }

            _destination.Complete();
        }

        public Task StartAsync(CancellationToken ct) => Task.CompletedTask;

        public Task ExecuteAsync(CancellationToken ct) => Process(ct);

        public Task StopAsync(CancellationToken forceExitToken) => Process(forceExitToken);
    }
}
