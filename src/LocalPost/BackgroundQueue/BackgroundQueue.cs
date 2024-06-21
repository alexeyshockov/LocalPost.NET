using System.Threading.Channels;
using LocalPost.AsyncEnumerable;
using LocalPost.DependencyInjection;

namespace LocalPost.BackgroundQueue;

internal static class BackgroundQueue
{
    // public static BackgroundQueue<T, ConsumeContext<T>> Create<T>(Options options) =>
    //     Create<T, ConsumeContext<T>>(options, reader => reader.ReadAllAsync());

    // public static BackgroundQueue<T, IReadOnlyCollection<ConsumeContext<T>>> CreateBatched<T>(
    //     BatchedOptions options) =>
    //     Create<T, IReadOnlyCollection<ConsumeContext<T>>>(options,
    //         reader => reader
    //             .ReadAllAsync()
    //             .Batch(ct =>
    //                 new BoundedBatchBuilder<ConsumeContext<T>>(options.BatchMaxSize, options.BatchTimeWindow, ct)),
    //         true);

    public static BackgroundQueue<T> Create<T>(IServiceProvider provider) =>
        Create(provider.GetOptions<QueueOptions<T>>());

    // To make the pipeline linear (single consumer), just add .ToConcurrent() to the end
    public static BackgroundQueue<T> Create<T>(QueueOptions<T> options)
    {
        // var channel = options.MaxSize switch
        // {
        //     not null => Channel.CreateBounded<ConsumeContext<T>>(new BoundedChannelOptions(options.MaxSize.Value)
        //     {
        //         SingleReader = options.MaxConcurrency == 1, // TODO Communicate SingleReader hint somehow...
        //         SingleWriter = false, // Accept it in options? Generally, we do not know how the queue will be used
        //         FullMode = options.FullMode,
        //     }),
        //     _ => Channel.CreateUnbounded<ConsumeContext<T>>(new UnboundedChannelOptions
        //     {
        //         SingleReader = options.MaxConcurrency == 1,
        //         SingleWriter = false, // We do not know how it will be used
        //     })
        // };
        var channel = options.Channel switch
        {
            BoundedChannelOptions channelOpt => Channel.CreateBounded<ConsumeContext<T>>(channelOpt),
            UnboundedChannelOptions channelOpt => Channel.CreateUnbounded<ConsumeContext<T>>(channelOpt),
            _ => throw new InvalidOperationException("Unknown channel options")
        };

        return new BackgroundQueue<T>(channel, TimeSpan.FromMilliseconds(options.CompletionDelay));
    }
}

internal sealed class BackgroundQueue<T>(Channel<ConsumeContext<T>> channel, TimeSpan completionDelay)
    : IBackgroundQueue<T>, IAsyncEnumerable<ConsumeContext<T>>, IBackgroundService
{
    public async IAsyncEnumerator<ConsumeContext<T>> GetAsyncEnumerator(CancellationToken cancellationToken)
    {
        var reader = channel.Reader;
        while (await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            while (reader.TryRead(out var item))
                yield return item;
    }

    // Track full or not later
    public ValueTask Enqueue(T item, CancellationToken ct = default) =>
        channel.Writer.WriteAsync(new ConsumeContext<T>(item), ct);

    public bool IsClosed { get; private set; }

    private async ValueTask CompleteAsync(CancellationToken ct = default)
    {
        if (IsClosed)
            return;

        if (completionDelay.TotalMilliseconds > 0)
            await Task.Delay(completionDelay, ct);

        channel.Writer.Complete();
        IsClosed = true;
    }

    public Task StartAsync(CancellationToken ct) => Task.CompletedTask;

    public Task ExecuteAsync(CancellationToken ct) => Task.CompletedTask;

    public async Task StopAsync(CancellationToken forceExitToken) => await CompleteAsync(forceExitToken);
}



// internal static partial class BackgroundQueue<T>
// {
//     public static readonly string Name = "BackgroundQueue/" + Reflection.FriendlyNameOf<T>();
// }
//
// internal sealed class BackgroundQueue<T, TOut>(
//     ChannelWriter<ConsumeContext<T>> input,
//     IAsyncEnumerable<TOut> pipeline,
//     TimeSpan completionDelay)
//     : IAsyncEnumerable<TOut>, IBackgroundService, IBackgroundQueue<T>
// {
//     public IAsyncEnumerator<TOut> GetAsyncEnumerator(CancellationToken ct) => pipeline.GetAsyncEnumerator(ct);
//
//     // Track full or not later
//     public ValueTask Enqueue(T item, CancellationToken ct = default) =>
//         input.WriteAsync(new ConsumeContext<T>(item), ct);
//
//     public bool IsClosed { get; private set; }
//
//     private async ValueTask CompleteAsync(CancellationToken ct = default)
//     {
//         if (IsClosed)
//             return;
//
//         if (completionDelay.TotalMilliseconds > 0)
//             await Task.Delay(completionDelay, ct);
//
//         input.Complete();
//         IsClosed = true;
//     }
//
//     public Task StartAsync(CancellationToken ct) => Task.CompletedTask;
//
//     public Task ExecuteAsync(CancellationToken ct) => pipeline switch
//     {
//         ConcurrentBuffer<TOut> concurrent => concurrent.Run(ct),
//         _ => Task.CompletedTask
//     };
//
//     public async Task StopAsync(CancellationToken forceExitToken) => await CompleteAsync(forceExitToken);
// }
