namespace LocalPost;

internal static class AsyncEnumerableEx
{
    public static IAsyncEnumerable<TOut> Batch<T, TOut>(this IAsyncEnumerable<T> source,
        BatchBuilderFactory<T, TOut> factory) => new BatchingAsyncEnumerable<T, TOut>(source, factory);

    public static IAsyncEnumerable<T> Merge<T>(this IEnumerable<IAsyncEnumerable<T>> sources) =>
        new AsyncEnumerableMerger<T>(sources);
}
