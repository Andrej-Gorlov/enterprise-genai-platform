using EnterpriseGenAI.DocumentIngestion.Worker.Contracts;

namespace EnterpriseGenAI.DocumentIngestion.Worker.Application;

public interface IIngestionMessageHandler
{
    Task HandleAsync(DocumentIngestionRequested message, CancellationToken cancellationToken);
}