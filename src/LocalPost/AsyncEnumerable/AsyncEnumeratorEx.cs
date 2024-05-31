using Nito.AsyncEx;

namespace LocalPost.AsyncEnumerable;

internal static class AsyncEnumeratorEx
{
    public static async ValueTask<T> Consume<T>(this IAsyncEnumerator<T> source, CancellationToken ct = default)
    {
        var waitTrigger = source.MoveNextAsync();
        var completed = waitTrigger.IsCompleted switch
        {
            true => await waitTrigger,
            _ => await waitTrigger.AsTask().WaitAsync(ct)
        };

        if (completed)
            // Ideally there should be a better way to communicate the completion...
            // But because it is usually used for long-running enumerators, fine
            throw new EndOfEnumeratorException("Source is empty");

        return source.Current;
    }
}

internal sealed class EndOfEnumeratorException(string message) : Exception(message);
