using EnterpriseGenAI.DocumentIngestion.Worker.Messaging;

namespace EnterpriseGenAI.DocumentIngestion.Worker;

public sealed class Worker(RabbitMqConsumer consumer, ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Document Ingestion Worker запущен");

        logger.LogWarning("Обработка входящих данных не реализована. Сообщения останутся без ответа.");

        try
        {
            await consumer.StartAsync(stoppingToken);
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Штатная остановка приложения.
        }
        finally
        {
            await consumer.DisposeAsync();
            logger.LogInformation("Document Ingestion Worker остановлен");
        }
    }
}