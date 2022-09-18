namespace LocalPost;

public interface IExecutor
{
    ValueTask Start(Func<Task> itemProcessor);

    // Wait for all active tasks to finish...
    ValueTask WaitAsync(CancellationToken forceExitToken);
}

internal sealed class BoundedExecutor : IExecutor
{
    private readonly List<Task> _tasks;

    public BoundedExecutor(int maxConcurrency = int.MaxValue)
    {
        MaxConcurrency = maxConcurrency;
        _tasks = new List<Task>(maxConcurrency);
    }

    public int MaxConcurrency { get; }

    // TODO Make it thread safe (lock?)
    public async ValueTask Start(Func<Task> itemProcessor)
    {
        CleanupCompleted();
        if (_tasks.Count >= MaxConcurrency)
            _tasks.Remove(await Task.WhenAny(_tasks));

        _tasks.Add(itemProcessor());
    }

    private void CleanupCompleted()
    {
        while (_tasks.Count > 0)
        {
            var waitForCompleted = Task.WhenAny(_tasks);
            if (!waitForCompleted.IsCompleted) // No completed tasks, so we need to wait
                return;

            _tasks.Remove(waitForCompleted.Result);
        }
    }

    public async ValueTask WaitAsync(CancellationToken forceExitToken)
    {
        var cancellationTask = Task.Delay(Timeout.Infinite, forceExitToken);
        while (_tasks.Count > 0)
        {
            var waitForCompleted = Task.WhenAny(_tasks.Append(cancellationTask));

            var completed = await waitForCompleted;
            if (completed == cancellationTask)
                return; // Exit has been forced

            _tasks.Remove(completed);
        }
    }
}
