using System.Threading.Channels;
using LocalPost.AsyncEnumerable;

namespace LocalPost.BackgroundQueues;

internal static class BackgroundQueue
{
    public static BackgroundQueue<T, ConsumeContext<T>> Create<T>(BackgroundQueueOptions options) =>
        Create<T, ConsumeContext<T>>(options, reader => reader.ReadAllAsync());

    public static BackgroundQueue<T, IReadOnlyList<ConsumeContext<T>>> CreateBatched<T>(
        BatchedBackgroundQueueOptions options) =>
        Create<T, IReadOnlyList<ConsumeContext<T>>>(options,
            reader => reader
                .ReadAllAsync()
                .Batch(ct =>
                    new BoundedBatchBuilder<ConsumeContext<T>>(options.BatchMaxSize, options.BatchTimeWindow, ct)),
            true);

    // To make the pipeline linear (single consumer), just add .ToConcurrent() to the end
    public static BackgroundQueue<T, TOut> Create<T, TOut>(BackgroundQueueOptions options,
        Func<ChannelReader<ConsumeContext<T>>, IAsyncEnumerable<TOut>> configure,
        bool proxy = false) // TODO Rename this parameter somehow...
    {
        var channel = options.MaxSize switch
        {
            not null => Channel.CreateBounded<ConsumeContext<T>>(new BoundedChannelOptions(options.MaxSize.Value)
            {
                SingleReader = proxy || options.MaxConcurrency == 1,
                SingleWriter = false, // We do not know how it will be used
                FullMode = options.FullMode,
            }),
            _ => Channel.CreateUnbounded<ConsumeContext<T>>(new UnboundedChannelOptions
            {
                SingleReader = proxy || options.MaxConcurrency == 1,
                SingleWriter = false, // We do not know how it will be used
            })
        };

        var pipeline = configure(channel.Reader);
        if (proxy)
            pipeline = pipeline.ToConcurrent();

        return new BackgroundQueue<T, TOut>(channel, pipeline,
            TimeSpan.FromMilliseconds(options.CompletionTimeout));
    }
}

internal static partial class BackgroundQueue<T>
{
    public static readonly string Name = "BackgroundQueue/" + Reflection.FriendlyNameOf<T>();
}

internal sealed class BackgroundQueue<T, TOut>(
    ChannelWriter<ConsumeContext<T>> input,
    IAsyncEnumerable<TOut> pipeline,
    TimeSpan completionDelay)
    : IAsyncEnumerable<TOut>, IBackgroundService, IBackgroundQueue<T>, IQueuePublisher<ConsumeContext<T>>
{
    public IAsyncEnumerator<TOut> GetAsyncEnumerator(CancellationToken ct) => pipeline.GetAsyncEnumerator(ct);

    // Track full or not later
    public ValueTask Enqueue(ConsumeContext<T> item, CancellationToken ct = default) => input.WriteAsync(item, ct);

    public ValueTask Enqueue(T item, CancellationToken ct = default) => Enqueue(new ConsumeContext<T>(item), ct);

    public bool IsClosed { get; private set; } // TODO Use

    private async ValueTask CompleteAsync(CancellationToken ct = default)
    {
        if (IsClosed)
            return;

        if (completionDelay.TotalMilliseconds > 0)
            await Task.Delay(completionDelay, ct);

        input.Complete();
        IsClosed = true;
    }

    public Task StartAsync(CancellationToken ct) => Task.CompletedTask;

    public Task ExecuteAsync(CancellationToken ct) => pipeline switch
    {
        ConcurrentAsyncEnumerable<TOut> concurrent => concurrent.Run(ct),
        _ => Task.CompletedTask
    };

    public async Task StopAsync(CancellationToken forceExitToken) => await CompleteAsync(forceExitToken);
}
