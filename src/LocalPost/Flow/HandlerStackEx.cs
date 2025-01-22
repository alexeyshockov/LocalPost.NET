using System.Collections.Immutable;
using System.Threading.Channels;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LocalPost.Flow;

[PublicAPI]
public static partial class HandlerStackEx
{
    // Keep it internal for now, until it's clear that this generic transformation is useful
    internal static HandlerManagerFactory<T> AsHandlerManager<T>(this HandlerFactory<T> hf) => provider =>
    {
        var handler = hf(provider);
        return new HandlerManager<T>(handler);
    };

    public static HandlerManagerFactory<T> Buffer<T>(this HandlerFactory<T> hf,
        int capacity, int consumers = 1, bool singleProducer = false) =>
        hf.AsHandlerManager().Buffer(capacity, consumers, singleProducer);

    public static HandlerManagerFactory<T> Batch<T>(this HandlerFactory<ImmutableArray<T>> hf,
        int size, TimeSpan window,
        int capacity = 1, int consumers = 1, bool singleProducer = false) =>
        hf.AsHandlerManager().Batch(size, window, capacity, consumers, singleProducer);
}

[PublicAPI]
public static partial class HandlerManagerStackEx
{
    public static HandlerManagerFactory<T> Buffer<T>(this HandlerManagerFactory<T> hmf,
        int capacity, int consumers = 1, bool singleProducer = false)
    {
        var channel = Channel.CreateBounded<T>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = consumers == 1,
            SingleWriter = singleProducer,
        });

        return hmf.Buffer(channel, consumers);
    }

    private static HandlerManagerFactory<T> Buffer<T>(this HandlerManagerFactory<T> hmf,
        Channel<T> channel, int consumers = 1) => provider =>
    {
        var next = hmf(provider);
        var buffer = new ChannelRunner<T, T>(channel, Consume, next) { Consumers = consumers };
        return new BufferHandlerManager<T>(channel, buffer);

        async Task Consume(CancellationToken execToken)
        {
            await foreach (var message in channel.Reader.ReadAllAsync(execToken).ConfigureAwait(false))
                await next.Handle(message, CancellationToken.None).ConfigureAwait(false);
        };
    };

    public static HandlerManagerFactory<T> Batch<T>(this HandlerManagerFactory<ImmutableArray<T>> hmf,
        int size, TimeSpan window,
        int capacity = 1, int consumers = 1, bool singleProducer = false) => provider =>
    {
        var next = hmf(provider);
        return BatchHandlerManager<T>.Create(next, size, window, capacity, consumers, singleProducer);
    };
}

internal sealed class BufferHandlerManager<T>(Channel<T> channel,
    ChannelRunner<T, T> runner) : IHandlerManager<T>
{
    public ValueTask Start(CancellationToken ct) => runner.Start(ct);

    public ValueTask Handle(T payload, CancellationToken ct) => channel.Writer.WriteAsync(payload, ct);

    public ValueTask Stop(Exception? error, CancellationToken ct) => runner.Stop(error, ct);
}

internal sealed class BatchHandlerManager<T>(Channel<T> channel,
    ChannelRunner<T, ImmutableArray<T>> runner) : IHandlerManager<T>
{
    public static BatchHandlerManager<T> Create(IHandlerManager<ImmutableArray<T>> next,
        int size, TimeSpan window,
        int capacity = 1, int consumers = 1, bool singleProducer = false)
    {
        var channel = Channel.CreateBounded<T>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = consumers == 1,
            SingleWriter = singleProducer,
        });
        // var handler = hf(provider);
        var buffer = new ChannelRunner<T, ImmutableArray<T>>(channel, Consume, next) { Consumers = consumers };
        var hm = new BatchHandlerManager<T>(channel, buffer);
        return hm;

        async Task Consume(CancellationToken execToken)
        {
            var reader = channel.Reader;

            var completed = false;
            var batchBuilder = ImmutableArray.CreateBuilder<T>(size);

            while (!completed)
            {
                using var timeWindowCts = CancellationTokenSource.CreateLinkedTokenSource(execToken);
                timeWindowCts.CancelAfter(window);
                try
                {
                    while (batchBuilder.Count < size)
                    {
                        var item = await reader.ReadAsync(timeWindowCts.Token).ConfigureAwait(false);
                        batchBuilder.Add(item);
                    }
                }
                catch (OperationCanceledException) when (!execToken.IsCancellationRequested)
                {
                    // Batch window is closed
                }
                catch (Exception) // execToken.IsCancellationRequested + ChannelClosedException
                {
                    completed = true;
                }

                if (batchBuilder.Count == 0)
                    continue;

                // If Capacity equals Count, the internal array will be extracted without copying the contents.
                // Otherwise, the contents will be copied into a new array. The internal buffer will then be set to a
                // zero length array.
                var batch = batchBuilder.DrainToImmutable();
                await next.Handle(batch, CancellationToken.None).ConfigureAwait(false);
            }
        };
    }

    public ValueTask Start(CancellationToken ct) => runner.Start(ct);

    public ValueTask Handle(T payload, CancellationToken ct) => channel.Writer.WriteAsync(payload, ct);

    public ValueTask Stop(Exception? error, CancellationToken ct) => runner.Stop(error, ct);
}

internal static class ChannelRunner
{
    // public static ChannelRunner<T, T> Create<T>(Channel<T> channel, Handler<Event<T>> handler,
    public static ChannelRunner<T, T> Create<T>(Channel<T> channel, IHandlerManager<T> handler,
        int consumers = 1, bool processLeftovers = true)
    {
        return new ChannelRunner<T, T>(channel, Consume, handler)
            { Consumers = consumers, ProcessLeftovers = processLeftovers };

        async Task Consume(CancellationToken execToken)
        {
            await foreach (var message in channel.Reader.ReadAllAsync(execToken).ConfigureAwait(false))
                await handler.Handle(message, CancellationToken.None).ConfigureAwait(false);
        }
    }
}

internal sealed class ChannelRunner<T, TOut>(Channel<T> channel,
    // Func<CancellationToken, Task> consumer, Handler<Event<TOut>> handler) : IDisposable
    Func<CancellationToken, Task> consumer, IHandlerManager<TOut> handler) : IDisposable
{
    public HealthCheckResult Ready => (_execTokenSource, _exec, _execException) switch
    {
        (null, _, _) => HealthCheckResult.Unhealthy("Not started"),
        (_, { IsCompleted: true }, _) => HealthCheckResult.Unhealthy("Stopped"),
        (not null, null, _) => HealthCheckResult.Degraded("Starting"),
        (not null, not null, null) => HealthCheckResult.Healthy("Running"),
        (_, _, not null) => HealthCheckResult.Unhealthy(null, _execException),
    };

    public PositiveInt Consumers { get; init; } = 1;
    public bool ProcessLeftovers { get; init; } = true;

    private CancellationTokenSource? _execTokenSource;
    private Task? _exec;
    private Exception? _execException;

    private CancellationToken _completionToken = CancellationToken.None;

    public async ValueTask Start(CancellationToken ct)
    {
        if (_execTokenSource is not null)
            throw new InvalidOperationException("Already started");

        var execTokenSource = _execTokenSource = new CancellationTokenSource();

        // await handler(Event<TOut>.Begin, ct).ConfigureAwait(false);
        await handler.Start(ct).ConfigureAwait(false);

        _exec = Run(execTokenSource.Token);
    }

    private async Task Run(CancellationToken execToken)
    {
        var exec = Consumers.Value switch
        {
            1 => RunConsumer(execToken),
            _ => Task.WhenAll(Enumerable.Range(0, Consumers).Select(_ => RunConsumer(execToken)))
        };
        await exec.ConfigureAwait(false);

        // await handler(Event<TOut>.End, _completionToken).ConfigureAwait(false);
        await handler.Stop(_execException, _completionToken).ConfigureAwait(false);
    }

    private async Task RunConsumer(CancellationToken execToken)
    {
        try
        {
            await consumer(execToken).ConfigureAwait(false);
            Close();
        }
        catch (OperationCanceledException e) when (e.CancellationToken == execToken)
        {
            // OK, fine
        }
        catch (ChannelClosedException)
        {
            // OK, fine
        }
        catch (Exception e)
        {
            Close(e);
        }
    }

    public async ValueTask Stop(Exception? e, CancellationToken ct)
    {
        _completionToken = ct;
        Close(e);
        if (_exec is not null)
            await _exec.ConfigureAwait(false);
    }

    public void Dispose()
    {
        _execTokenSource?.Dispose();
        _exec?.Dispose();
    }

    private void Close(Exception? e = null)
    {
        if (!channel.Writer.TryComplete(e))
            return;
        _execException ??= e;
        if (!ProcessLeftovers)
            _execTokenSource?.Cancel();
    }
}
