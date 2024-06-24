using System.Runtime.CompilerServices;
using System.Threading.Channels;
using JetBrains.Annotations;
using LocalPost.AsyncEnumerable;
using LocalPost.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LocalPost;

[PublicAPI]
public static class HandlerStack<T>
{
    public static readonly HandlerFactory<T> Empty = _ => (_, _) => default;
}

[PublicAPI]
public static class HandlerStack
{
    public static HandlerFactory<T> For<T>(Handler<T> handler) => _ => handler;

    public static HandlerFactory<T> From<THandler, T>() where THandler : IHandler<T> =>
        provider => provider.GetRequiredService<THandler>().InvokeAsync;
}



[PublicAPI]
public static class Pipeline
{
    public sealed record ConsumerOptions(ushort MaxConcurrency = 1, bool BreakOnException = false);

    internal sealed class Consumer<T>(
        ILogger<StreamProcessor<T>> logger,
        Handler<T> handler,
        ushort maxConcurrency = 1,
        bool breakOnException = false)
    {
        public async Task Consume(IAsyncEnumerable<T> queue, CancellationToken execCt)
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
                    // It is either:
                    //  - app shutdown timeout (force shutdown)
                    //  - handler exception (when BreakOnException is set)
                    // Just break the loop
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
                    if (breakOnException)
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
    }

    internal static PipelineRegistration<T> Create<T>(HandlerFactory<T> hf,
        ushort maxConcurrency, bool breakOnException) =>
        Create(hf, _ => new ConsumerOptions(maxConcurrency, breakOnException));

    internal static PipelineRegistration<T> Create<T>(HandlerFactory<T> hf,
        Func<IServiceProvider, ConsumerOptions> config) => (context, pf) =>
        context.Services.AddBackgroundService(provider =>
        {
            var stream = pf(provider);
            var (maxConcurrency, breakOnException) = config(provider);
            var consumer = new Consumer<T>(
                provider.GetRequiredService<ILogger<StreamProcessor<T>>>(),
                hf(provider),
                maxConcurrency,
                breakOnException);

            return new StreamRunner<T>(stream, consumer.Consume)
            {
                Target = context.Target,
            };
        });
}

[PublicAPI]
internal static class PipelineOps
{
    public static PipelineRegistration<T> Where<T>(this PipelineRegistration<T> next,
        Func<T, bool> pred)
    {
        return next.Map<T, T>(Filter);

        async IAsyncEnumerable<T> Filter(IAsyncEnumerable<T> source, [EnumeratorCancellation] CancellationToken ct)
        {
            await foreach (var item in source.WithCancellation(ct))
                if (pred(item))
                    yield return item;
        }
    }

    // TODO Option with a service from the DI provider (add IPipelineMiddleware interface)
    public static PipelineRegistration<TIn> Map<TIn, TOut>(this PipelineRegistration<TOut> next,
        PipelineMiddleware<TIn, TOut> middleware) =>
        (services, pf) => next(services, pf.Map(middleware));

    public static PipelineFactory<TOut> Map<TIn, TOut>(this PipelineFactory<TIn> pf,
        PipelineMiddleware<TIn, TOut> middleware) => provider =>
    {
        var source = pf(provider);
        return middleware(source);
    };

    private sealed class SharedBuffer<T>(Func<IServiceProvider, int> config)
    {
        private Channel<T>? _buffer;

        public Channel<T> GetOrCreate(IServiceProvider provider)
        {
            if (_buffer is not null)
                return _buffer;

            var capacity = config(provider);
            return _buffer = Channel.CreateBounded<T>(new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                // This is the point in most of the cases, like batching, to have a simple source reader to a buffer,
                // so that buffer can be read by multiple consumers
                SingleReader = false,
                SingleWriter = true,
            });

        }
    }

    public static PipelineRegistration<T> Buffer<T>(this PipelineRegistration<T> next, ushort capacity) =>
        next.Buffer(_ => capacity);

    public static PipelineRegistration<T> Buffer<T>(this PipelineRegistration<T> next,
        Func<IServiceProvider, int> config)
    {
        var sharedBuffer = new SharedBuffer<T>(config);

        return (context, source) =>
        {
            var (services, target) = context;

            services.AddBackgroundService(provider =>
            {
                // Freeze (resolve) the current pipeline
                var stream = source(provider);
                // And drain it to the channel, in the background
                return new StreamRunner<T>(stream, BufferWriter(provider))
                {
                    Target = target,
                };
            });

            // Create a new pipeline, from the channel
            next(context, provider =>
            {
                var buffer = sharedBuffer.GetOrCreate(provider);
                return buffer.Reader.ReadAllAsync();
            });
        };

        StreamProcessor<T> BufferWriter(IServiceProvider provider)
        {
            var buffer = sharedBuffer.GetOrCreate(provider);

            return async (source, ct) =>
            {
                try
                {
                    await foreach (var item in source.WithCancellation(ct))
                        await buffer.Writer.WriteAsync(item, ct);
                }
                finally
                {
                    buffer.Writer.Complete();
                }
            };
        }
    }

    // public static PipelineRegistration<T> Buffer<T>(this PipelineRegistration<T> next, int capacity = 1)
    // {
    //     var buffer = Channel.CreateBounded<T>(new BoundedChannelOptions(capacity)
    //     {
    //         FullMode = BoundedChannelFullMode.Wait,
    //         SingleReader = false, // Configure somehow...
    //         SingleWriter = true,
    //     });
    //
    //     return (context, source) =>
    //     {
    //         var (services, target) = context;
    //
    //         services.AddBackgroundService(provider =>
    //         {
    //             // Freeze (resolve) the current pipeline
    //             var stream = source(provider);
    //             // And drain it to the channel, in the background
    //             return new PipelineRunner<T>(stream, BufferWriter(provider))
    //             {
    //                 Target = target,
    //             };
    //         });
    //
    //         // Create a new pipeline, from the channel
    //         next(context, provider =>
    //         {
    //             var buffer = sharedBuffer.GetOrCreate(provider);
    //             return buffer.Reader.ReadAllAsync();
    //         });
    //     };
    //
    //     async Task WriteToBuffer(IAsyncEnumerable<T> source, CancellationToken ct)
    //     {
    //         try
    //         {
    //             await foreach (var item in source.WithCancellation(ct))
    //                 await buffer.Writer.WriteAsync(item, ct);
    //         }
    //         finally
    //         {
    //             buffer.Writer.Complete();
    //         }
    //     }
    // }

    public static PipelineRegistration<T> Batch<T>(this PipelineRegistration<IEnumerable<T>> next,
        ushort batchMaxSize = 10, int timeWindowDuration = 1_000) =>
        next.Batch(_ => new BatchOptions(batchMaxSize, timeWindowDuration));

    public static PipelineRegistration<T> Batch<T>(this PipelineRegistration<IEnumerable<T>> next,
        Func<IServiceProvider, BatchOptions> config) => (context, source) => next(context, provider =>
    {
        var stream = source(provider);
        var (batchMaxSize, timeWindowDuration) = config(provider);
        return stream.Batch(() => new BoundedBatchBuilder<T>(batchMaxSize, timeWindowDuration));
    });
}
