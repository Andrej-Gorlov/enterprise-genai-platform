using EnterpriseGenAI.DocumentIngestion.Worker.Contracts;

namespace EnterpriseGenAI.DocumentIngestion.Worker.Application;

public sealed class IngestionMessageHandler(ILogger<IngestionMessageHandler> logger) : IIngestionMessageHandler
{
    public Task HandleAsync(DocumentIngestionRequested message, CancellationToken cancellationToken)
    {
        // Временный компромисс: только диагностика, обработка не завершена, ack нет.
        logger.LogWarning("Ingestion not implemented yet. JobId={JobId} DocumentId={DocumentId} ContentVersion={ContentVersion} ProcessingVersion={ProcessingVersion}. Message left unacked.",
            message.JobId,
            message.DocumentId,
            message.ContentVersion,
            message.ProcessingVersion);
        return Task.CompletedTask;
    }
}