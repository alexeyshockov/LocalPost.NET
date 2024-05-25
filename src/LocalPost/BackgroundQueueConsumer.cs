using LocalPost.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LocalPost;

internal sealed record ConsumerOptions(ushort MaxConcurrency, bool BreakOnException);

internal static class BackgroundQueue
{
    // public static ConsumerGroup<TQ, T> ConsumerGroupFor<T, TQ>(TQ queue, Handler<T> handler, ushort maxConcurrency)
    //     where TQ : IAsyncEnumerable<T> => new(Consumer.LoopOver(queue, handler), maxConcurrency);
    //
    // public static NamedConsumerGroup<TQ, T> ConsumerGroupForNamed<T, TQ>(
    //     TQ queue, Handler<T> handler, ushort maxConcurrency)
    //     where TQ : IAsyncEnumerable<T>, INamedService =>
    //     new(queue, Consumer.LoopOver(queue, handler), maxConcurrency);

    // Parametrized class, to be used with the Dependency Injection container
    internal class NamedConsumer<TQ, T>(
        ILogger<ConsumerBase<T>> logger,
        TQ queue,
        Handler<T> handler,
        ushort maxConcurrency)
        : ConsumerBase<T>(logger, queue, handler, maxConcurrency), INamedService
        where TQ : IAsyncEnumerable<T>, INamedService
    {
        public string Name { get; } = queue.Name;
    }

    // Parametrized class, to be used with the Dependency Injection container
    internal class Consumer<TQ, T>(
        ILogger<ConsumerBase<T>> logger,
        TQ queue,
        Handler<T> handler,
        ushort maxConcurrency)
        : ConsumerBase<T>(logger, queue, handler, maxConcurrency)
        where TQ : IAsyncEnumerable<T>;

    internal abstract class ConsumerBase<T>(
        ILogger<ConsumerBase<T>> logger,
        IAsyncEnumerable<T> queue,
        Handler<T> handler,
        ushort maxConcurrency)
        : IBackgroundService //, IDisposable
    {
        public bool BreakOnException { get; init; } = false;
        // private bool _broken = false;

        private Task? _exec;
        private CancellationTokenSource? _execCts;

        private async Task Execute(CancellationToken execCt)
        {
            // using var loopCts = new CancellationTokenSource();
            using var loopCts = CancellationTokenSource.CreateLinkedTokenSource(execCt);
            // using var cts = CancellationTokenSource.CreateLinkedTokenSource(execCt, loopCts.Token);
            var loopCt = loopCts.Token;

            await Task.WhenAll(Enumerable.Range(1, maxConcurrency)
                .Select(_ => Loop()));

            return;

            async Task Loop()
            {
                try
                {
                    await foreach (var message in queue.WithCancellation(loopCt))
                        await Handle(message);
                }
                catch (OperationCanceledException) when (loopCt.IsCancellationRequested)
                {
                    // Fine, breaking the loop because of an exception in the handler
                }
            }

            async Task Handle(T message)
            {
                try
                {
                    await handler(message, execCt);
                }
                catch (OperationCanceledException) when (execCt.IsCancellationRequested)
                {
                    throw; // App shutdown timeout (force shutdown)
                }
                catch (Exception e)
                {
                    if (BreakOnException)
                    {
                        // Break the loop (all the concurrent executions of it)
                        // ReSharper disable once AccessToDisposedClosure
                        loopCts.Cancel();
                        // Push it up, so the service is marked as unhealthy
                        throw;
                    }

                    logger.LogError(e, "Failed to handle a message");
                }
            }
        }

        public Task StartAsync(CancellationToken ct) => Task.CompletedTask;

        public Task ExecuteAsync(CancellationToken ct)
        {
            if (_exec is not null)
                return _exec;

            var execCts = _execCts = new CancellationTokenSource();
            return _exec = Execute(execCts.Token);
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
}
