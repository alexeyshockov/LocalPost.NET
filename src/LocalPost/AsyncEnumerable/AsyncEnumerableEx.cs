namespace LocalPost.AsyncEnumerable;

internal static class AsyncEnumerableEx
{
    // TODO Better name...
    public static ConcurrentAsyncEnumerable<T> ToConcurrent<T>(this IAsyncEnumerable<T> source,
        MaxSize bufferMaxSize = default) => new(source, bufferMaxSize);

    public static IAsyncEnumerable<TOut> Batch<T, TOut>(this IAsyncEnumerable<T> source,
        BatchBuilderFactory<T, TOut> factory) => new BatchingAsyncEnumerable<T, TOut>(source, factory);

    public static IAsyncEnumerable<T> Merge<T>(this IEnumerable<IAsyncEnumerable<T>> sources) =>
        new AsyncEnumerableMerger<T>(sources);
}
