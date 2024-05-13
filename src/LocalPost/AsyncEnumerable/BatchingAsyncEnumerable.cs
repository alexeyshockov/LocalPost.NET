namespace LocalPost.AsyncEnumerable;

internal sealed class BatchingAsyncEnumerable<T, TOut> : IAsyncEnumerable<TOut>
{
    private readonly IAsyncEnumerable<T> _reader;
    private readonly BatchBuilderFactory<T, TOut> _factory;

    public BatchingAsyncEnumerable(IAsyncEnumerable<T> source, BatchBuilderFactory<T, TOut> factory)
    {
        _reader = source;
        _factory = factory;

    }

//    public async IAsyncEnumerator<TOut> GetAsyncEnumerator_old(CancellationToken ct = default)
//    {
//        // FIXME To static builder...
//        using var batchBuilder = _factory(ct);
//
//        var source = _reader.GetAsyncEnumerator(ct);
//        var completed = false;
//        var waitTrigger = source.MoveNextAsync();
//        Task<bool>? waitTask = null;
//        while (!completed && !ct.IsCancellationRequested)
//        {
//            var shift = false;
//            try
//            {
//                if (waitTask is null && waitTrigger.IsCompleted)
//                    completed = !await waitTrigger;
//                else
//                {
//                    waitTask ??= waitTrigger.AsTask();
//                    // To save some allocations?..
////                    completed = !await (await Task.WhenAny(waitTask, batchBuilder.TimeWindowTrigger));
//                    completed = !await waitTask.WaitAsync(batchBuilder.TimeWindow);
//                    waitTask = null;
//                }
//
//                if (completed)
//                    continue;
//            }
//            catch (OperationCanceledException e) when (e.CancellationToken == batchBuilder.TimeWindow)
//            {
//                shift = true;
//            }
//            catch (OperationCanceledException) // User (global) cancellation
//            {
//                continue;
//            }
//
//            if (shift)
//            { // C# doesn't allow "yield return" in a try/catch block...
//                if (!batchBuilder.IsEmpty)
//                    yield return batchBuilder.Flush();
//
//                continue;
//            }
//
//            if (!batchBuilder.TryAdd(source.Current))
//                if (!batchBuilder.IsEmpty)
//                {
//                    // Flush the current buffer and start a fresh one
//                    yield return batchBuilder.Flush();
//                    if (!batchBuilder.TryAdd(source.Current))
//                        HandleSkipped(source.Current); // Even an empty batch cannot fit it...
//                }
//                else
//                    HandleSkipped(source.Current); // Even an empty buffer cannot fit it...
//
//            waitTrigger = source.MoveNextAsync();
//        }
//
//        // Flush on completion or error...
//        if (!batchBuilder.IsEmpty)
//            yield return batchBuilder.Flush();
//
//        ct.ThrowIfCancellationRequested();
//    }

    public async IAsyncEnumerator<TOut> GetAsyncEnumerator(CancellationToken ct = default)
    {
        await using var source = _reader.GetAsyncEnumerator(ct);
        using var batchBuilder = _factory(ct);
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
