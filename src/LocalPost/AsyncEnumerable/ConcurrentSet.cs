using System.Collections;
using System.Collections.Immutable;
using Nito.AsyncEx;

namespace LocalPost.AsyncEnumerable;

internal sealed class ConcurrentSet<T> : IEnumerable<T>, IDisposable
{
    private readonly object _modificationLock = new();
    private ImmutableHashSet<T>? _elements;
    private CancellationTokenSource _modificationTriggerSource = new();
    private CancellationTokenTaskSource<bool> _modificationTriggerTaskSource;

    public ConcurrentSet(IEnumerable<T> sources)
    {
        _modificationTriggerTaskSource = new CancellationTokenTaskSource<bool>(_modificationTriggerSource.Token);
        _elements = sources.ToImmutableHashSet();
    }

    public ImmutableHashSet<T> Elements => _elements ?? throw new ObjectDisposedException(nameof(ConcurrentSet<T>));

    public CancellationToken ModificationToken => _modificationTriggerSource.Token;

    public Task Modification => _modificationTriggerTaskSource.Task;

    private ImmutableHashSet<T> ChangeSources(Func<ImmutableHashSet<T>, ImmutableHashSet<T>> change)
    {
        ImmutableHashSet<T> changedSources;
        CancellationTokenSource trigger;
        CancellationTokenTaskSource<bool> triggerTask;
        lock (_modificationLock)
        {
            changedSources = change(Elements);
            if (changedSources == _elements)
                return _elements; // Nothing has changed

            _elements = changedSources;
            trigger = _modificationTriggerSource;
            triggerTask = _modificationTriggerTaskSource;
            _modificationTriggerSource = new CancellationTokenSource();
            _modificationTriggerTaskSource = new CancellationTokenTaskSource<bool>(_modificationTriggerSource.Token);
        }

        trigger.Cancel(); // Notify about the modification

        triggerTask.Dispose();
        trigger.Dispose();

        return changedSources;
    }

    public ImmutableHashSet<T> Add(T source) => ChangeSources(sources => sources.Add(source));

    public ImmutableHashSet<T> Remove(T source) => ChangeSources(sources => sources.Remove(source));

    public IEnumerator<T> GetEnumerator() => Elements.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void Dispose()
    {
        _modificationTriggerSource.Dispose();
        _modificationTriggerTaskSource.Dispose();
        _elements = null;
    }
}
