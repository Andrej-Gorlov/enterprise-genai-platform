namespace EnterpriseGenAI.DocumentIngestion.Worker;

public sealed class Worker(ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Document Ingestion Worker запущен");

        try
        {
            // Ждём отмены без активного опроса и без пустого while цикла
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Штатная остановка по CancellationToken
        }

        logger.LogInformation("Document Ingestion Worker остановлен");
    }
}