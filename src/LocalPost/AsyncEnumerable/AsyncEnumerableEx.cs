using System.Collections.Immutable;

namespace LocalPost.AsyncEnumerable;

internal static class AsyncEnumerableEx
{
    public static IAsyncEnumerable<ImmutableArray<T>> Batch<T>(this IAsyncEnumerable<T> source,
        int maxSize, TimeSpan timeWindow) => new BatchingAsyncEnumerable<T>(source, maxSize, timeWindow);

    public static IAsyncEnumerable<ImmutableArray<T>> Batch<T>(this IAsyncEnumerable<T> source,
        int maxSize, int timeWindowMs) => Batch(source, maxSize, TimeSpan.FromMilliseconds(timeWindowMs));

    public static IAsyncEnumerable<T> Merge<T>(this IEnumerable<IAsyncEnumerable<T>> sources) =>
        new AsyncEnumerableMerger<T>(sources);
}
