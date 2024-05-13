using LocalPost.DependencyInjection;

namespace LocalPost;

//    public async Task StopAsync(CancellationToken forceExitToken)
//    {
//        // Do not cancel the execution immediately, as it will finish gracefully itself (when the channel is closed)
//
//        // TODO .NET 6 async...
//        using var linked = forceExitToken.Register(() => _executionCts?.Cancel());
//
//        if (_execution is not null)
//            await _execution;
//    }



internal static partial class BackgroundQueue
{
//    public static BackgroundQueue<T>.Consumer ConsumerFor<T>(IAsyncEnumerable<T> reader, Handler<T> handler) =>
//        new(reader, handler);

//    public static IBackgroundServiceSupervisor ConsumerSupervisorFor<T>(BackgroundQueue<T>.Consumer consumer) =>
//        new BackgroundServiceSupervisor(consumer);

    public static ConsumerGroup<TQ, T> ConsumerGroupFor<T, TQ>(TQ queue, Handler<T> handler, int maxConcurrency)
        where TQ : IAsyncEnumerable<T> => new(Consumer.Loop(queue, handler), maxConcurrency);

    public static ConsumerGroup<TQ, T> ConsumerGroupOverDisposablesFor<T, TQ>(TQ queue, Handler<T> handler, int maxConcurrency)
        where TQ : IAsyncEnumerable<T>
        where T : IDisposable => new(Consumer.LoopOverDisposables(queue, handler), maxConcurrency);

    public static NamedConsumerGroup<TQ, T> ConsumerGroupForNamed<T, TQ>(TQ queue, Handler<T> handler, int maxConcurrency)
        where TQ : IAsyncEnumerable<T>, INamedService => new(queue, Consumer.Loop(queue, handler), maxConcurrency);

    public static NamedConsumerGroup<TQ, T> ConsumerGroupOverDisposablesForNamed<T, TQ>(TQ queue, Handler<T> handler, int maxConcurrency)
        where TQ : IAsyncEnumerable<T>, INamedService
        where T : IDisposable => new(queue, Consumer.Loop(queue, handler), maxConcurrency);

    internal class NamedConsumerGroup<TQ, T> : ConsumerGroupBase<T>, INamedService
        where TQ : IAsyncEnumerable<T>, INamedService
    {
        public NamedConsumerGroup(TQ queue, Func<CancellationToken, Task> loop, int maxConcurrency) :
            base(loop, maxConcurrency)
        {
            Name = queue.Name;
        }

        public string Name { get; }
    }

    // Parametrized class, to be used with the Dependency Injection container
    internal class ConsumerGroup<TQ, T> : ConsumerGroupBase<T> where TQ : IAsyncEnumerable<T>
    {
        public ConsumerGroup(Func<CancellationToken, Task> loop, int maxConcurrency) : base(loop, maxConcurrency)
        {
        }
    }

    // Parametrized class, to be used with the Dependency Injection container
    internal class ConsumerGroupBase<T> : IBackgroundService
    {
        private readonly List<Consumer> _consumers;

        protected ConsumerGroupBase(Func<CancellationToken, Task> loop, int maxConcurrency)
        {
            _consumers = Enumerable.Range(1, maxConcurrency)
                .Select(_ => new Consumer(loop))
                .ToList();
        }

        public Task StartAsync(CancellationToken ct) => Task.CompletedTask;

        public Task ExecuteAsync(CancellationToken ct) =>
            Task.WhenAll(_consumers.Select(c => c.ExecuteAsync(ct)));

        public Task StopAsync(CancellationToken ct) =>
            Task.WhenAll(_consumers.Select(c => c.StopAsync(ct)));
    }

    internal sealed class Consumer
    {
        public static Func<CancellationToken, Task> Loop<T>(IAsyncEnumerable<T> queue, Handler<T> handler) =>
            async (CancellationToken ct) =>
            {
                await foreach (var message in queue.WithCancellation(ct))
                    await handler(message, ct);
            };

        public static Func<CancellationToken, Task> LoopOverDisposables<T>
            (IAsyncEnumerable<T> queue, Handler<T> handler) where T : IDisposable =>
            async (CancellationToken ct) =>
            {
                await foreach (var message in queue.WithCancellation(ct))
                    try
                    {
                        await handler(message, ct);
                    }
                    finally
                    {
                        message.Dispose();
                    }
            };

        private readonly Func<CancellationToken, Task> _loop;

        public Consumer(Func<CancellationToken, Task> loop)
        {
            _loop = loop;
        }

        public Task ExecuteAsync(CancellationToken ct) => _loop(ct);

        public Task StopAsync(CancellationToken ct) => _loop(ct); // Process the rest (leftovers)
    }
}
