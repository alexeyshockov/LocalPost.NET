using System.Collections.Immutable;
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
    private readonly TimeSpan _timeWindowDuration;
    private readonly CancellationToken _ct;

    private CancellationTokenSource _timeWindow;
    private CancellationTokenTaskSource<bool>? _timeWindowTrigger;

    protected BatchBuilderBase(TimeSpan timeWindowDuration, CancellationToken ct = default)
    {
        _timeWindowDuration = timeWindowDuration;
        _ct = ct; // Rename to globalCancellation or something like that...

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
            return new CancellationTokenSource(_timeWindowDuration);

        var timeWindow = CancellationTokenSource.CreateLinkedTokenSource(_ct);
        timeWindow.CancelAfter(_timeWindowDuration);

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
    private readonly int _batchMaxSize;
    protected ImmutableArray<T>.Builder Batch;

    protected BoundedBatchBuilderBase(MaxSize batchMaxSize, TimeSpan timeWindowDuration, CancellationToken ct = default) :
        base(timeWindowDuration, ct)
    {
        _batchMaxSize = batchMaxSize;
        Batch = ImmutableArray.CreateBuilder<T>(_batchMaxSize);
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
        Batch = ImmutableArray.CreateBuilder<T>(_batchMaxSize);
    }
}

internal sealed class BoundedBatchBuilder<T>(MaxSize batchMaxSize, TimeSpan timeWindowDuration, CancellationToken ct = default)
    : BoundedBatchBuilderBase<T, IReadOnlyList<T>>(batchMaxSize, timeWindowDuration, ct)
{
    public override IReadOnlyList<T> Build() => Batch; // TODO ImmutableArray
}
