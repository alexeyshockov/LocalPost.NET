using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;

namespace LocalPost.AsyncEnumerable;

internal sealed class AsyncEnumerableMerger<T> : IAsyncEnumerable<T>, IDisposable
{
    private readonly ConcurrentSet<IAsyncEnumerable<T>> _sources;

    public AsyncEnumerableMerger(bool permanent = false) : this(ImmutableArray<IAsyncEnumerable<T>>.Empty, permanent)
    {
    }

    public AsyncEnumerableMerger(IEnumerable<IAsyncEnumerable<T>> sources, bool permanent = false)
    {
        if (permanent)
            // This one IAsyncEnumerable will be there forever, so the wait will be indefinite (even if all other
            // sources are completed)
            sources = sources.Prepend(Channel.CreateUnbounded<T>(new UnboundedChannelOptions
            {
                SingleReader = false,
                SingleWriter = true
            }).Reader.ReadAllAsync());

        _sources = new ConcurrentSet<IAsyncEnumerable<T>>(sources);
    }

    public void Add(IAsyncEnumerable<T> source) => _sources.Add(source);

    [SuppressMessage("ReSharper", "PossibleMultipleEnumeration")]
    public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken ct = default)
    {
        async Task<(IAsyncEnumerable<T>, IAsyncEnumerator<T>, bool)> Wait(IAsyncEnumerable<T> source, IAsyncEnumerator<T> enumerator) =>
            (source, enumerator, !await enumerator.MoveNextAsync());

        var sourcesSnapshot = _sources.Elements;
        var waits = sourcesSnapshot
            .Select(source => Wait(source, source.GetAsyncEnumerator(ct)))
            .ToImmutableArray();

        while (waits.Length > 0)
        {
            var modificationTrigger = Task.Delay(Timeout.Infinite, _sources.ModificationToken);
            var waitTrigger = Task.WhenAny(waits);

            await Task.WhenAny(waitTrigger, modificationTrigger);

            if (waitTrigger.IsCompleted) // New element
            {
                var completedWait = await waitTrigger;
                var (source, enumerator, completed) = await completedWait;

                if (!completed)
                {
                    yield return enumerator.Current;
                    waits = waits.Replace(completedWait, Wait(source, enumerator));
                }
                else
                {
                    waits = waits.Remove(completedWait);
                    sourcesSnapshot = _sources.Remove(source);
                }
            }

            // Always check modification trigger explicitly, as both task can complete when the control is back
            // (in this case we can miss the trigger completely)
            // ReSharper disable once InvertIf
            if (modificationTrigger.IsCompleted)
                // Wait() _somehow_ can give the control away, so loop until we sure that all the changes are handled
                while (sourcesSnapshot != _sources.Elements)
                {
                    var previousSourcesSnapshot = sourcesSnapshot;
                    sourcesSnapshot = _sources.Elements;

                    // ReSharper disable once ForeachCanBeConvertedToQueryUsingAnotherGetEnumerator
                    foreach (var newSource in sourcesSnapshot.Except(previousSourcesSnapshot))
                        waits = waits.Add(Wait(newSource, newSource.GetAsyncEnumerator(ct)));
                }
        }
    }

    public void Dispose()
    {
        _sources.Dispose();
    }
}
