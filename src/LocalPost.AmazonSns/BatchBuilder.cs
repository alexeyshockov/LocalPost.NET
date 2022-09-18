namespace LocalPost.AmazonSns;

public delegate IBatchBuilder<T, TBatch> BatchBuilderFactory<in T, out TBatch>();

public interface IBatchBuilder<in T, out TBatch>
{
    CancellationToken TimeWindow { get; }

    bool IsEmpty { get; }

    bool Add(T entry);

    TBatch BuildAndDispose();
}
