using EnterpriseGenAI.DocumentIngestion.Worker.Contracts;

namespace EnterpriseGenAI.DocumentIngestion.Worker.Application;

// Атомарный захват задания по сообщению с классификацией исхода
public interface IIngestionJobClaimer
{
    Task<IngestionClaimResult> TryClaimAsync(DocumentIngestionRequested message, CancellationToken cancellationToken);
}