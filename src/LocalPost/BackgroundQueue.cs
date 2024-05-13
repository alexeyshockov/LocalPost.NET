using System.Threading.Channels;
using JetBrains.Annotations;
using LocalPost.AsyncEnumerable;

namespace LocalPost;

[PublicAPI]
public interface IBackgroundQueue<in T>
{
    // TODO Custom exception when closed?.. Or just return true/false?..
    ValueTask Enqueue(T item, CancellationToken ct = default);
}

internal static partial class BackgroundQueue
{
    public static BackgroundQueue<T, T> Create<T>(BackgroundQueueOptions options) =>
        Create<T, T>(options, reader => reader.ReadAllAsync());

    public static BackgroundQueue<T, IReadOnlyList<T>> CreateBatched<T>(BatchedBackgroundQueueOptions options) =>
        Create<T, IReadOnlyList<T>>(options,
            reader => reader
                .ReadAllAsync()
                .Batch(ct => new BoundedBatchBuilder<T>(options.BatchMaxSize, options.BatchTimeWindow, ct)),
            true);

    // To make the pipeline linear (single consumer), just add .ToConcurrent() to the end
    public static BackgroundQueue<T, TOut> Create<T, TOut>(BackgroundQueueOptions options,
        Func<ChannelReader<T>, IAsyncEnumerable<TOut>> configure,
        bool proxy = false) // TODO Rename this parameter somehow...
    {
        var channel = options.MaxSize switch
        {
            not null => Channel.CreateBounded<T>(new BoundedChannelOptions(options.MaxSize.Value)
            {
                SingleReader = proxy || options.MaxConcurrency == 1,
                SingleWriter = false, // We do not know how it will be used
                FullMode = options.FullMode,
            }),
            _ => Channel.CreateUnbounded<T>(new UnboundedChannelOptions
            {
                SingleReader = proxy || options.MaxConcurrency == 1,
                SingleWriter = false, // We do not know how it will be used
            })
        };

        var pipeline = configure(channel.Reader);
        if (proxy)
            pipeline = pipeline.ToConcurrent();

        return new BackgroundQueue<T, TOut>(channel, pipeline,
            TimeSpan.FromMilliseconds(options.CompletionTimeout ?? 0));
    }
}

internal static partial class BackgroundQueue<T>
{
    public static readonly string Name = "BackgroundQueue/" + Reflection.FriendlyNameOf<T>();
}

internal sealed class BackgroundQueue<T, TOut> : IBackgroundQueue<T>, IAsyncEnumerable<TOut>,
    IBackgroundService
{
    private readonly TimeSpan _completionTimeout;
    private readonly ChannelWriter<T> _messages;
    private readonly IAsyncEnumerable<TOut> _pipeline;

    public BackgroundQueue(ChannelWriter<T> input, IAsyncEnumerable<TOut> pipeline, TimeSpan completionTimeout)
    {
        _completionTimeout = completionTimeout;
        _messages = input;
        _pipeline = pipeline;
    }

    public IAsyncEnumerator<TOut> GetAsyncEnumerator(CancellationToken ct) => _pipeline.GetAsyncEnumerator(ct);

    // Track full or not later
    public ValueTask Enqueue(T item, CancellationToken ct = default) => _messages.WriteAsync(item, ct);

    public bool IsClosed { get; private set; } // TODO Use

    private async ValueTask CompleteAsync(CancellationToken ct = default)
    {
        if (IsClosed)
            return;

        await Task.Delay(_completionTimeout, ct);

        _messages.Complete();
        IsClosed = true;
    }

    public Task StartAsync(CancellationToken ct) => Task.CompletedTask;

    public Task ExecuteAsync(CancellationToken ct) => _pipeline switch
    {
        ConcurrentAsyncEnumerable<TOut> concurrent => concurrent.Run(ct),
        _ => Task.CompletedTask
    };

    public async Task StopAsync(CancellationToken forceExitToken) => await CompleteAsync(forceExitToken);
}





//// Open to public later?..
//internal interface IBackgroundQueueManager
//{
//    // Implement later for a better health check?..
////    bool IsFull { get; }
//
//    bool IsClosed { get; }
//
//    ValueTask CompleteAsync(CancellationToken ct = default);
//}
