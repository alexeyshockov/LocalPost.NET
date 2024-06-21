using LocalPost.DependencyInjection;

namespace LocalPost;

internal interface IStreamRunner : IBackgroundService, IAssistantService;

internal sealed class StreamRunner<T>(IAsyncEnumerable<T> stream, StreamProcessor<T> consume) : IStreamRunner
{
    public required AssistedService Target { get; init; }

    private Task? _exec;
    private CancellationTokenSource? _execCts;

    public Task StartAsync(CancellationToken ct) => Task.CompletedTask;

    public Task ExecuteAsync(CancellationToken ct)
    {
        if (_exec is not null)
            return _exec;

        var execCts = _execCts = new CancellationTokenSource();
        return _exec = consume(stream, execCts.Token);
    }

    // Process the rest (leftovers). Common cases:
    //  - SQS: message source (fetcher) has been stopped, so we just need to process leftovers from the channel
    //  - Kafka: message source (consumer) has been stopped, so we just need to process leftovers from the channel
    //  - Background (job) queue: hope that the producers are stopped, so no new messages should appear, so we
    //    just need to process leftovers from the queue
    public Task StopAsync(CancellationToken ct)
    {
        if (_exec is null)
            return Task.CompletedTask;

        ct.Register(() => _execCts?.Cancel());
        return _exec;

        // Cleanup the state?..
    }
}



// internal sealed record ConsumerOptions(ushort MaxConcurrency, bool BreakOnException);
//
// internal static class Queue
// {
//     internal interface IConsumer : IBackgroundService, IServiceFor;
//
//     internal sealed class Consumer<T>(
//         ILogger<Consumer<T>> logger,
//         IAsyncEnumerable<T> queue,
//         Handler<T> handler,
//         ushort maxConcurrency)
//         : IConsumer //, IDisposable
//     {
//         public required string Target { get; init; }
//
//         public bool BreakOnException { get; init; } = false;
//         // private bool _broken = false;
//
//         private Task? _exec;
//         private CancellationTokenSource? _execCts;
//
//         private async Task Execute(CancellationToken execCt)
//         {
//             // using var loopCts = new CancellationTokenSource();
//             using var loopCts = CancellationTokenSource.CreateLinkedTokenSource(execCt);
//             // using var cts = CancellationTokenSource.CreateLinkedTokenSource(execCt, loopCts.Token);
//             var loopCt = loopCts.Token;
//
//             await Task.WhenAll(Enumerable.Range(1, maxConcurrency)
//                 .Select(_ => Loop()));
//
//             return;
//
//             async Task Loop()
//             {
//                 try
//                 {
//                     await foreach (var message in queue.WithCancellation(loopCt))
//                         await Handle(message);
//                 }
//                 catch (OperationCanceledException) when (loopCt.IsCancellationRequested)
//                 {
//                     // It is either:
//                     //  - app shutdown timeout (force shutdown)
//                     //  - handler exception (when BreakOnException is set)
//                     // Just break the loop
//                 }
//             }
//
//             async Task Handle(T message)
//             {
//                 try
//                 {
//                     await handler(message, execCt);
//                 }
//                 catch (OperationCanceledException) when (execCt.IsCancellationRequested)
//                 {
//                     throw; // App shutdown timeout (force shutdown)
//                 }
//                 catch (Exception e)
//                 {
//                     if (BreakOnException)
//                     {
//                         // Break the loop (all the concurrent executions of it)
//                         // ReSharper disable once AccessToDisposedClosure
//                         loopCts.Cancel();
//                         // Push it up, so the service is marked as unhealthy
//                         throw;
//                     }
//
//                     logger.LogError(e, "Failed to handle a message");
//                 }
//             }
//         }
//
//         public Task StartAsync(CancellationToken ct) => Task.CompletedTask;
//
//         public Task ExecuteAsync(CancellationToken ct)
//         {
//             if (_exec is not null)
//                 return _exec;
//
//             var execCts = _execCts = new CancellationTokenSource();
//             return _exec = Execute(execCts.Token);
//         }
//
//         // Process the rest (leftovers). Common cases:
//         //  - SQS: message source (fetcher) has been stopped, so we just need to process leftovers from the channel
//         //  - Kafka: message source (consumer) has been stopped, so we just need to process leftovers from the channel
//         //  - Background (job) queue: hope that the producers are stopped, so no new messages should appear, so we
//         //    just need to process leftovers from the queue
//         public Task StopAsync(CancellationToken ct)
//         {
//             if (_exec is null)
//                 return Task.CompletedTask;
//
//             ct.Register(() => _execCts?.Cancel());
//             return _exec;
//
//             // Cleanup the state?..
//         }
//     }
// }
