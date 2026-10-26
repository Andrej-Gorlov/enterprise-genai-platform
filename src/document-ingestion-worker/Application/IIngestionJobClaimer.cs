using EnterpriseGenAI.DocumentIngestion.Worker.Contracts;

namespace EnterpriseGenAI.DocumentIngestion.Worker.Application;

public interface IIngestionJobClaimer
{
    Task<IngestionClaimResult> TryClaimAsync(DocumentIngestionRequested message, CancellationToken cancellationToken);
}