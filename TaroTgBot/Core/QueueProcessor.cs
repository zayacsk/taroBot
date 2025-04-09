namespace TaroTgBot.Core;

public class QueueProcessor
{
    private readonly TaskQueue _taskQueue;
    private readonly CancellationToken _cancellationToken;

    public QueueProcessor(TaskQueue taskQueue, CancellationToken cancellationToken)
    {
        _taskQueue = taskQueue;
        _cancellationToken = cancellationToken;
    }

    public async Task StartProcessingAsync()
    {
        while (!_cancellationToken.IsCancellationRequested)
        {
            var task = await _taskQueue.DequeueAsync(_cancellationToken);
            if (task != null)
            {
                try
                {
                    await task();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка обработки задачи: {ex.Message}");
                }
            }
        }
    }
}