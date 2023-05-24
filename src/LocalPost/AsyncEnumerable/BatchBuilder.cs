using Nito.AsyncEx;

namespace LocalPost.AsyncEnumerable;

internal delegate IBatchBuilder<T, TBatch> BatchBuilderFactory<in T, out TBatch>();

internal interface IBatchBuilder<in T, out TBatch> : IDisposable
{
    CancellationToken TimeWindow { get; }
    Task<bool> TimeWindowTrigger { get; }

    bool IsEmpty { get; }

    bool TryAdd(T entry);

    TBatch Build();
}

internal abstract class BatchBuilder<T, TBatch> : IBatchBuilder<T, TBatch>
{
    private readonly CancellationTokenSource _timeWindow;
    private readonly CancellationTokenTaskSource<bool> _timeWindowTrigger;

    protected BatchBuilder(TimeSpan timeWindow)
    {
        _timeWindow = new CancellationTokenSource(timeWindow);
        _timeWindowTrigger = new CancellationTokenTaskSource<bool>(_timeWindow.Token);
    }

    public CancellationToken TimeWindow => _timeWindow.Token;
    public Task<bool> TimeWindowTrigger => _timeWindowTrigger.Task;
    public abstract bool IsEmpty { get; }

    public abstract bool TryAdd(T entry);
    public abstract TBatch Build();

    public void Dispose()
    {
        _timeWindow.Dispose();
        _timeWindowTrigger.Dispose();
    }
}

internal sealed class BoundedBatchBuilder<T> : BatchBuilder<T, IReadOnlyList<T>>
{
    public static BatchBuilderFactory<T, IReadOnlyList<T>> Factory(int maxSize, int timeWindow) =>
        () => new BoundedBatchBuilder<T>(maxSize, TimeSpan.FromMilliseconds(timeWindow));

    private readonly int _max;
    private readonly List<T> _batch = new();

    public BoundedBatchBuilder(int max, TimeSpan timeWindow) : base(timeWindow)
    {
        _max = max;
    }

    public override bool IsEmpty => _batch.Count == 0;

    public override bool TryAdd(T entry)
    {
        if (_batch.Count >= _max)
            return false;

        _batch.Add(entry);

        return true;
    }

    public override IReadOnlyList<T> Build() => _batch;
}
