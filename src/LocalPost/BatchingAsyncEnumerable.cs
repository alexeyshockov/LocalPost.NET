namespace LocalPost;

internal sealed class BatchingAsyncEnumerable<T, TOut> : IAsyncEnumerable<TOut>
{
    private readonly IAsyncEnumerable<T> _reader;
    private readonly BatchBuilderFactory<T, TOut> _factory;

    public BatchingAsyncEnumerable(IAsyncEnumerable<T> source, BatchBuilderFactory<T, TOut> factory)
    {
        _reader = source;
        _factory = factory;

    }

    private void HandleSkipped(T item)
    {
    }

    public async IAsyncEnumerator<TOut> GetAsyncEnumerator(CancellationToken ct = default)
    {
        var batch = _factory();
        IBatchBuilder<T, TOut> ShiftBatch()
        {
            batch.Dispose();
            return batch = _factory();
        }

        try
        {
            var source = _reader.GetAsyncEnumerator(ct);
            var completed = false;
            var waitTrigger = source.MoveNextAsync();
            Task<bool>? waitTask = null;
            while (!completed)
            {
                var shift = false;
                try
                {
                    if (waitTask is null && waitTrigger.IsCompleted)
                        completed = !await waitTrigger;
                    else
                    {
                        waitTask ??= waitTrigger.AsTask();
                        // The same as Task.WaitAsync(batch.TimeWindow), but saves some allocations
                        completed = !await (await Task.WhenAny(waitTask, batch.TimeWindowTrigger));
                        waitTask = null;
                    }

                    if (completed)
                        continue;
                }
                catch (OperationCanceledException e) when (e.CancellationToken == batch.TimeWindow)
                {
                    shift = true;
                }
                catch (OperationCanceledException) // User cancellation
                {
                    completed = true;
                    continue;
                }

                if (shift)
                {
                    if (!batch.IsEmpty)
                        yield return batch.Build();

                    ShiftBatch();
                    continue;
                }

                if (!batch.TryAdd(source.Current))
                    if (!batch.IsEmpty)
                    {
                        // Flush the current buffer and start a fresh one
                        yield return batch.Build();
                        if (!ShiftBatch().TryAdd(source.Current))
                            HandleSkipped(source.Current); // Even an empty buffer cannot fit it...
                    }
                    else
                        HandleSkipped(source.Current); // Even an empty buffer cannot fit it...

                waitTrigger = source.MoveNextAsync();
            }

            // Flush on completion or error...
            if (!batch.IsEmpty)
                yield return batch.Build();

            ct.ThrowIfCancellationRequested();
        }
        finally
        {
            batch.Dispose();
        }
    }
}
