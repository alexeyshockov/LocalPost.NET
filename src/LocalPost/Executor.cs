using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Options;

namespace LocalPost;

internal interface IExecutor
{
    public bool IsEmpty { get; }

    // Start only when there is a capacity
    ValueTask StartAsync(Func<Task> itemProcessor, CancellationToken ct);

    // Wait for all active tasks to finish...
    ValueTask WaitAsync(CancellationToken ct);
}

internal sealed class BoundedExecutor : IExecutor
{
    private readonly ConcurrentTasksList _tasks;

    public BoundedExecutor(string name, IOptionsMonitor<ConsumerOptions> options) : this(options.Get(name).MaxConcurrency)
    {
    }

    public BoundedExecutor(ushort maxConcurrency = ushort.MaxValue)
    {
        MaxConcurrency = maxConcurrency;
        _tasks = new ConcurrentTasksList(MaxConcurrency);
    }

    public ushort MaxConcurrency { get; }

    public bool IsEmpty => _tasks.Count == 0;

    private async ValueTask WaitForCapacityAsync(CancellationToken ct)
    {
        _tasks.CleanupCompleted();
        while (_tasks.Count >= MaxConcurrency)
            await _tasks.WaitForCompleted(ct);
    }

    public async ValueTask StartAsync(Func<Task> itemProcessor, CancellationToken ct)
    {
        if (_tasks.Count >= MaxConcurrency)
            await WaitForCapacityAsync(ct);

        _tasks.Track(itemProcessor());
    }

    public async ValueTask WaitAsync(CancellationToken ct)
    {
        _tasks.CleanupCompleted();
        while (!IsEmpty)
            await _tasks.WaitForCompleted(ct);
    }
}
