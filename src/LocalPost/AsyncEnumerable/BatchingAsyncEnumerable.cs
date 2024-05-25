namespace LocalPost.AsyncEnumerable;

internal sealed class BatchingAsyncEnumerable<T, TOut>(
    IAsyncEnumerable<T> reader, BatchBuilderFactory<T, TOut> factory) : IAsyncEnumerable<TOut>
{
    public async IAsyncEnumerator<TOut> GetAsyncEnumerator(CancellationToken ct = default)
    {
        await using var source = reader.GetAsyncEnumerator(ct);
        using var batchBuilder = factory(ct);
        while (!ct.IsCancellationRequested)
        {
            TOut completedBatch;
            try
            {
                var consumeResult = await source.Consume(ct); // FIXME batchBuilder.TimeWindow
                var added = batchBuilder.TryAdd(consumeResult);
                if (!added)
                {
                    completedBatch = batchBuilder.Flush();
                    batchBuilder.TryAdd(consumeResult); // TODO If a message does not fit in an empty batch...
                }
                else
                {
                    if (batchBuilder.Full)
                        completedBatch = batchBuilder.Flush();
                    else
                        continue;
                }
            }
            catch (EndOfEnumeratorException)
            {
                break;
            }
            // Batch time window is closed, or the cancellation token is triggered
            catch (OperationCanceledException)
            {
                if (batchBuilder.IsEmpty)
                    continue;

                completedBatch = batchBuilder.Flush();
            }

            yield return completedBatch;
        }

        if (!batchBuilder.IsEmpty)
            yield return batchBuilder.Flush();

        ct.ThrowIfCancellationRequested();
    }
}
