using System.Collections.Immutable;
using System.Threading.Channels;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LocalPost.Flow;


[PublicAPI]
public static partial class HandlerStackEx
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
        new BufferHandlerManager<T>(hmf(provider), channel, consumers);

    public static HandlerManagerFactory<T> Batch<T>(this HandlerManagerFactory<IReadOnlyCollection<T>> hmf,
        int size, TimeSpan window,
        int capacity = 1, bool singleProducer = false) => provider =>
    {
        var channel = Channel.CreateBounded<T>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = singleProducer,
        });
        var next = hmf(provider);
        return new BatchHandlerManager<T>(next, channel, size, window);
    };
}

internal sealed class BufferHandlerManager<T>(IHandlerManager<T> next, Channel<T> channel,
    int consumers) : IHandlerManager<T>
{
    private ChannelRunner<T, T>? _runner;

    private ChannelRunner<T, T> CreateRunner(Handler<T> handler)
    {
        return new ChannelRunner<T, T>(channel, Consume, next) { Consumers = consumers };

        async Task Consume(CancellationToken execToken)
        {
            await foreach (var message in channel.Reader.ReadAllAsync(execToken).ConfigureAwait(false))
                await handler(message, CancellationToken.None).ConfigureAwait(false);
        }
    }

    public async ValueTask<Handler<T>> Start(CancellationToken ct)
    {
        var handler = await next.Start(ct).ConfigureAwait(false);
        _runner = CreateRunner(handler);
        await _runner.Start(ct).ConfigureAwait(false);
        return channel.Writer.WriteAsync;
    }

    public async ValueTask Stop(Exception? error, CancellationToken ct)
    {
        if (_runner is not null)
            await _runner.Stop(error, ct).ConfigureAwait(false);
        await next.Stop(error, ct).ConfigureAwait(false);
    }
}

internal sealed class BatchHandlerManager<T>(IHandlerManager<IReadOnlyCollection<T>> next, Channel<T> channel,
    int size, TimeSpan window) : IHandlerManager<T>
{
    private ChannelRunner<T, IReadOnlyCollection<T>>? _runner;

    private ChannelRunner<T, IReadOnlyCollection<T>> CreateRunner(Handler<IReadOnlyCollection<T>> handler)
    {
        return new ChannelRunner<T, IReadOnlyCollection<T>>(channel, Consume, next) { Consumers = 1 };

        async Task Consume(CancellationToken execToken)
        {
            var completed = false;
            while (!completed)
            {
                var batch = new List<T>(size);
                using var timeWindowCts = CancellationTokenSource.CreateLinkedTokenSource(execToken);
                timeWindowCts.CancelAfter(window);
                try
                {
                    while (batch.Count < size)
                    {
                        var item = await channel.Reader.ReadAsync(timeWindowCts.Token).ConfigureAwait(false);
                        batch.Add(item);
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

                if (batch.Count == 0)
                    continue;

                await handler(batch, CancellationToken.None).ConfigureAwait(false);
            }
        }
    }

    public async ValueTask<Handler<T>> Start(CancellationToken ct)
    {
        var handler = await next.Start(ct).ConfigureAwait(false);
        _runner = CreateRunner(handler);
        await _runner.Start(ct).ConfigureAwait(false);
        return channel.Writer.WriteAsync;
    }

    public async ValueTask Stop(Exception? error, CancellationToken ct)
    {
        if (_runner is not null)
            await _runner.Stop(error, ct).ConfigureAwait(false);
        await next.Stop(error, ct).ConfigureAwait(false);
    }
}

internal sealed class ChannelRunner<T, TOut>(Channel<T> channel,
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
