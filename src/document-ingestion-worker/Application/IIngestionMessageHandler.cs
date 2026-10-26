using EnterpriseGenAI.DocumentIngestion.Worker.Contracts;

namespace EnterpriseGenAI.DocumentIngestion.Worker.Application;

// Обрабатывает сообщение о запросе ingestion документа
public interface IIngestionMessageHandler
{
    Task HandleAsync(DocumentIngestionRequested message, CancellationToken cancellationToken);
}