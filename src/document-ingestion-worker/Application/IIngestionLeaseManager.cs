using EnterpriseGenAI.DocumentIngestion.Worker.Contracts;

namespace EnterpriseGenAI.DocumentIngestion.Worker.Application;

public interface IIngestionLeaseManager
{
    Task<DateTime?> TryRenewAsync(DocumentIngestionRequested message, Guid leaseToken, CancellationToken cancellationToken);
}