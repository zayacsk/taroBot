using System.Collections.Concurrent;

namespace TaroTgBot.Core;

public class TaskQueue
{
    private readonly ConcurrentQueue<Func<Task>> _taskQueue = new();
    private readonly SemaphoreSlim _signal = new(0);

    public void Enqueue(Func<Task> task)
    {
        _taskQueue.Enqueue(task);
        _signal.Release();
    }

    public async Task<Func<Task>?> DequeueAsync(CancellationToken cancellationToken)
    {
        await _signal.WaitAsync(cancellationToken);
        _taskQueue.TryDequeue(out var task);
        return task;
    }
}