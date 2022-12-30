using Nito.AsyncEx;

namespace LocalPost;

internal sealed class ConcurrentTasksList
{
    private readonly object _tasksLock = new();
    private readonly List<Task> _tasks;

    public ConcurrentTasksList(int capacityHint)
    {
        _tasks = new List<Task>(capacityHint);
    }

    public int Count => _tasks.Count;

    private Task<Task> WhenAny()
    {
        lock (_tasksLock)
            return Task.WhenAny(_tasks);
    }

    private void Remove(Task item)
    {
        lock (_tasksLock)
            _tasks.Remove(item);
    }

    public void CleanupCompleted()
    {
        lock (_tasksLock)
            _tasks.RemoveAll(task => task.IsCompleted);
    }

    public void Track(Task item)
    {
        lock (_tasksLock)
            _tasks.Add(item);
    }

    public async Task WaitForCompleted(CancellationToken ct) => Remove(await WhenAny().WaitAsync(ct));
}
