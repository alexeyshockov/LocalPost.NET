using System.Collections.Immutable;
using System.Threading.Channels;
using LocalPost.AsyncEnumerable;

namespace LocalPost;

internal sealed class ConsumerSupervisor : IBackgroundServiceSupervisor
{
    private CancellationTokenSource? _executionCts;
    private Task? _execution;

    private readonly Func<CancellationToken, Task> _consumer;

    public ConsumerSupervisor(Func<CancellationToken, Task> consumer)
    {
        _consumer = consumer;
    }

    public bool Started => _executionCts is not null && _execution is not null;

    public bool Running => _execution is not null && !_execution.IsCompleted;

    public bool Crashed => Exception is not null;

    public Exception? Exception { get; private set; }

    public Task StartAsync(CancellationToken ct)
    {
        if (_executionCts is not null)
            throw new InvalidOperationException("Execution has been already started");

        _executionCts = new CancellationTokenSource();
        _execution = ExecuteAsync(_executionCts.Token);

        return Task.CompletedTask;
    }

    private async Task ExecuteAsync(CancellationToken ct)
    {
        if (ct.IsCancellationRequested)
            return;

        try
        {
            await _consumer(ct);

            // Completed
        }
        catch (OperationCanceledException e) when (e.CancellationToken == ct)
        {
            // Completed gracefully on request
        }
        catch (Exception e)
        {
            Exception = e;
        }
    }

    public async Task StopAsync(CancellationToken forceExitToken)
    {
        // Do not cancel the execution immediately, as it will finish gracefully itself (when the channel is closed)

        // TODO .NET 6 async...
        using var linked = forceExitToken.Register(() => _executionCts?.Cancel());

        if (_execution is not null)
            await _execution;
    }

    public void Dispose()
    {
        _executionCts?.Dispose();
        _execution?.Dispose();
    }
}

// With predefined static size
internal sealed class ConsumerGroup : IBackgroundServiceSupervisor
{
    private readonly ImmutableArray<ConsumerSupervisor> _services;

    public ConsumerGroup(Func<CancellationToken, Task> consumer, int maxConcurrency)
    {
        _services = Enumerable.Range(1, maxConcurrency)
            .Select(_ => new ConsumerSupervisor(consumer))
            .ToImmutableArray();
    }

    public Task StartAsync(CancellationToken ct) =>
        Task.WhenAll(_services.Select(service => service.StartAsync(ct)));
    // TODO Log info

    public Task StopAsync(CancellationToken ct) =>
        Task.WhenAll(_services.Select(service => service.StopAsync(ct)));
    // TODO Log info

    public bool Started => _services.All(c => c.Started);
    public bool Running => _services.All(c => c.Running);
    public bool Crashed => _services.Any(c => c.Crashed);
    public Exception? Exception => null; // TODO Implement

    public void Dispose()
    {
        foreach (var disposable in _services)
            disposable.Dispose();
    }
}

internal sealed partial class BackgroundQueue<T>
{
    internal sealed class Consumer
    {
        private readonly IAsyncEnumerable<T> _reader;
        private readonly Handler<T> _handler;

        public Consumer(IAsyncEnumerable<T> reader, Handler<T> handler)
        {
            _reader = reader;
            _handler = handler;
        }

        public async Task Run(CancellationToken ct)
        {
            await foreach (var message in _reader.WithCancellation(ct))
                await _handler(message, ct);
        }
    }

    internal sealed class BatchConsumer<TOut>
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

        public async Task Run(CancellationToken ct)
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

        private async Task ProcessBatch(CancellationToken ct)
        {
            await _destination.WriteAsync(_batch.Build(), ct);
            _batch.Dispose();
            _batch = _factory();
        }
    }
}
