using System.Collections.Immutable;
using System.Threading.Channels;

namespace LocalPost.AmazonSns;

public static partial class ChannelReader // System.Threading.Channels defines Channel static class already
{
    public static CombinedChannelReader<T> Combine<T>(IEnumerable<ChannelReader<T>> readers) => new(readers);
}

public sealed class CombinedChannelReader<T> : ChannelReader<T>, IDisposable
{
    private ImmutableList<ChannelReader<T>>? _sources = ImmutableList<ChannelReader<T>>.Empty;
    private CancellationTokenSource _modificationTrigger = new();

    public CombinedChannelReader()
    {
    }

    public CombinedChannelReader(IEnumerable<ChannelReader<T>> sources)
    {
        Sources = sources.ToImmutableList();
    }

    private ImmutableList<ChannelReader<T>> Sources
    {
        get => _sources ?? throw new ObjectDisposedException(nameof(CombinedChannelReader<T>));
        set => _sources = value;
    }

    // TODO TryAdd() to complement this one
    public void Add(ChannelReader<T> source)
    {
        _sources = Sources.Add(source);
        var trigger = _modificationTrigger;
        _modificationTrigger = new CancellationTokenSource();
        trigger.Cancel(); // Activate the trigger
        trigger.Dispose();
    }

    // Actually not needed, they will be removed in WaitToReadAsync() anyway
//    public void CleanupCompleted()
//    {
//        _sources = _sources.Where(reader => !reader.Completion.IsCompleted).ToImmutableList();
//    }

    public override bool TryRead(out T item)
    {
        foreach (var source in Sources)
        {
            var read = source.TryRead(out item!);
            if (read) return true;
        }

        item = default!;
        return false;
    }

    public override async ValueTask<bool> WaitToReadAsync(CancellationToken cancellationToken = default)
    {
        async Task<(ChannelReader<T>, bool)> WaitFor(ChannelReader<T> reader, ValueTask<bool> waitTrigger) =>
            (reader, await waitTrigger);

        do
        {
            var modificationTrigger = _modificationTrigger.Token;
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, modificationTrigger);

            var triggers = new List<Task<(ChannelReader<T>, bool)>>();
            foreach (var reader in Sources)
            {
                var waitTrigger = reader.WaitToReadAsync(cts.Token);
                if (waitTrigger.IsCompleted)
                    return await waitTrigger;

                triggers.Add(WaitFor(reader, waitTrigger));
            }

            var trigger = await Task.WhenAny(triggers);
            try
            {
                var (reader, waitResult) = await trigger;
                if (!waitResult)
                    // Exclude a reader if completed... And continue waiting.
                    Sources = Sources.Remove(reader);

                return true;
            }
            catch (OperationCanceledException e) when (e.CancellationToken == modificationTrigger)
            {
                // Continue waiting
            }
        } while (true);
    }

    public void Dispose()
    {
        _modificationTrigger.Dispose();
        _sources = null;
    }
}
