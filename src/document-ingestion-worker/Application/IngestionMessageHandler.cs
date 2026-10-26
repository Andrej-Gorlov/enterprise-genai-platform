using EnterpriseGenAI.DocumentIngestion.Worker.Contracts;

namespace EnterpriseGenAI.DocumentIngestion.Worker.Application;

public sealed class IngestionMessageHandler(IIngestionJobClaimer claimer, ILogger<IngestionMessageHandler> logger) : IIngestionMessageHandler
{
    public async Task HandleAsync(DocumentIngestionRequested message, CancellationToken cancellationToken)
    {
        var result = await claimer.TryClaimAsync(message, cancellationToken);

        if (result is IngestionClaimResult.Acquired acquired)
        {
            logger.LogInformation(
                "Задача по приему данных взята в обработку. JobId={JobId}, LeaseUntil={LeaseUntil:O}. Сообщение оставлено без подтверждения получения (unacked).", acquired.JobId, acquired.LeaseUntil);
            
            return;
        }

        logger.LogInformation("Задача по приему данных не была получена. JobId={JobId}. Сообщение осталось без подтверждения получения.", message.JobId);
    }
}