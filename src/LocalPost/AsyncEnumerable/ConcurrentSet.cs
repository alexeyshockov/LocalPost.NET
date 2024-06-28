using System.Collections;
using System.Collections.Immutable;

namespace LocalPost.AsyncEnumerable;

internal sealed class ConcurrentSet<T>(IEnumerable<T> sources) : IEnumerable<T>, IDisposable
{
    private readonly object _modificationLock = new();
    private ImmutableHashSet<T>? _elements = sources.ToImmutableHashSet();
    private CancellationTokenSource _modificationTriggerSource = new();

    public ImmutableHashSet<T> Elements => _elements ?? throw new ObjectDisposedException(nameof(ConcurrentSet<T>));

    public CancellationToken ModificationToken => _modificationTriggerSource.Token;

    private ImmutableHashSet<T> ChangeSources(Func<ImmutableHashSet<T>, ImmutableHashSet<T>> change)
    {
        ImmutableHashSet<T> changedSources;
        CancellationTokenSource trigger;
        lock (_modificationLock)
        {
            changedSources = change(Elements);
            if (changedSources == _elements)
                return _elements; // Nothing has changed

            _elements = changedSources;
            trigger = _modificationTriggerSource;
            _modificationTriggerSource = new CancellationTokenSource();
        }

        trigger.Cancel(); // Notify about the modification
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
        _elements = null;
    }
}
