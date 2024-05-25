using Nito.AsyncEx;

namespace LocalPost.AsyncEnumerable;

internal delegate IBatchBuilder<T, TBatch> BatchBuilderFactory<in T, out TBatch>(CancellationToken ct = default);

internal interface IBatchBuilder<in T, out TBatch> : IDisposable
{
    CancellationToken TimeWindow { get; }
    bool TimeWindowClosed { get; }
    Task<bool> TimeWindowTrigger { get; }

    bool IsEmpty { get; }
    bool Full { get; }

    bool TryAdd(T entry);

    TBatch Build();
    void Reset();
    TBatch Flush();
}

internal abstract class BatchBuilderBase<T, TBatch> : IBatchBuilder<T, TBatch>
{
    private readonly TimeSpan _timeWindowLength;
    private readonly CancellationToken _ct;

    private CancellationTokenSource _timeWindow;
    private CancellationTokenTaskSource<bool>? _timeWindowTrigger;

    protected BatchBuilderBase(TimeSpan timeWindow, CancellationToken ct = default)
    {
        _timeWindowLength = timeWindow;
        _ct = ct; // TODO Rename to globalCancellation or something like that

        _timeWindow = StartTimeWindow();
    }

    public CancellationToken TimeWindow => _timeWindow.Token;
    public bool TimeWindowClosed => TimeWindow.IsCancellationRequested;
    public Task<bool> TimeWindowTrigger =>
        (_timeWindowTrigger ??= new CancellationTokenTaskSource<bool>(_timeWindow.Token)).Task;

    public abstract bool IsEmpty { get; }
    public abstract bool Full { get; }

    public abstract bool TryAdd(T entry);

    public abstract TBatch Build();

    private CancellationTokenSource StartTimeWindow()
    {
        if (_ct == CancellationToken.None)
            return new CancellationTokenSource(_timeWindowLength);

        var timeWindow = CancellationTokenSource.CreateLinkedTokenSource(_ct);
        timeWindow.CancelAfter(_timeWindowLength);

        return timeWindow;
    }

    // Should be overwritten in derived classes, to reset their state also
    public virtual void Reset()
    {
        _timeWindow.Cancel();
        _timeWindow.Dispose();
        _timeWindow = StartTimeWindow();

        _timeWindowTrigger?.Dispose();
        _timeWindowTrigger = null;
    }

    public TBatch Flush()
    {
        var batch = Build();
        Reset();
        return batch;
    }

    public void Dispose()
    {
        _timeWindow.Dispose();
        _timeWindowTrigger?.Dispose();
    }
}

internal abstract class BoundedBatchBuilderBase<T, TBatch> : BatchBuilderBase<T, TBatch>
{
    private readonly MaxSize _batchMaxSize;
    protected List<T> Batch; // FIXME ImmutableArrayBuilder

    protected BoundedBatchBuilderBase(MaxSize batchMaxSize, TimeSpan timeWindow, CancellationToken ct = default) :
        base(timeWindow, ct)
    {
        _batchMaxSize = batchMaxSize;
        Batch = new List<T>(_batchMaxSize);
    }

    public override bool IsEmpty => Batch.Count == 0;

    public override bool Full => Batch.Count >= _batchMaxSize;

    public override bool TryAdd(T entry)
    {
        if (Full)
            return false;

        Batch.Add(entry);

        return true;
    }

    public override void Reset()
    {
        base.Reset();
        Batch = new List<T>(_batchMaxSize);
    }
}

internal sealed class BoundedBatchBuilder<T> : BoundedBatchBuilderBase<T, IReadOnlyList<T>>
{
    public BoundedBatchBuilder(MaxSize batchMaxSize, TimeSpan timeWindow, CancellationToken ct = default) :
        base(batchMaxSize, timeWindow, ct)
    {
    }

    public override IReadOnlyList<T> Build() => Batch;
}
