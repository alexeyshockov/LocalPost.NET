using System.Collections.Immutable;
using Nito.AsyncEx;

namespace LocalPost.AsyncEnumerable;

internal delegate IBatchBuilder<T, TBatch> BatchBuilderFactory<in T, out TBatch>();

internal interface IBatchBuilder<in T, out TBatch> : IDisposable
{
    CancellationToken TimeWindow { get; }
    Task TimeWindowTrigger { get; }

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

    private CancellationTokenSource _timeWindow;
    private CancellationTokenTaskSource<bool>? _timeWindowTrigger;

    protected BatchBuilderBase(TimeSpan timeWindowDuration)
    {
        _timeWindowDuration = timeWindowDuration;
        _timeWindow = StartTimeWindow();
    }

    public CancellationToken TimeWindow => _timeWindow.Token;
    public bool TimeWindowClosed => TimeWindow.IsCancellationRequested;
    public Task TimeWindowTrigger =>
        (_timeWindowTrigger ??= new CancellationTokenTaskSource<bool>(_timeWindow.Token)).Task;

    public abstract bool IsEmpty { get; }
    public abstract bool Full { get; }

    public abstract bool TryAdd(T entry);

    public abstract TBatch Build();

    private CancellationTokenSource StartTimeWindow() => new(_timeWindowDuration);

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

    public virtual void Dispose()
    {
        _timeWindow.Dispose();
        _timeWindowTrigger?.Dispose();
    }
}

internal abstract class BoundedBatchBuilderBase<T, TBatch> : BatchBuilderBase<T, TBatch>
{
    private readonly int _batchMaxSize;
    protected List<T> Batch;

    protected BoundedBatchBuilderBase(MaxSize batchMaxSize, TimeSpan timeWindowDuration) :
        base(timeWindowDuration)
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

internal sealed class BoundedBatchBuilder<T>(MaxSize batchMaxSize, TimeSpan timeWindowDuration)
    : BoundedBatchBuilderBase<T, IReadOnlyCollection<T>>(batchMaxSize, timeWindowDuration)
{
    public BoundedBatchBuilder(MaxSize batchMaxSize, int timeWindowDuration)
        : this(batchMaxSize, TimeSpan.FromMilliseconds(timeWindowDuration))
    {
    }

    public override IReadOnlyCollection<T> Build() => Batch; // ImmutableArray or something?..
}
