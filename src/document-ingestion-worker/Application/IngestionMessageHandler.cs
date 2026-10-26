using EnterpriseGenAI.DocumentIngestion.Worker.Contracts;

namespace EnterpriseGenAI.DocumentIngestion.Worker.Application;

public sealed class IngestionMessageHandler(IIngestionJobClaimer claimer, ILogger<IngestionMessageHandler> logger) : IIngestionMessageHandler
{
    public async Task HandleAsync(DocumentIngestionRequested message, CancellationToken cancellationToken)
    {
        var result = await claimer.TryClaimAsync(message, cancellationToken);

        switch (result)
        {
            case IngestionClaimResult.Acquired acquired:
                logger.LogInformation(
                    "Задание взято в обработку. JobId={JobId}, LeaseUntil={LeaseUntil:O}. Сообщение оставлено без подтверждения.", acquired.JobId, acquired.LeaseUntil);
                return;

            case IngestionClaimResult.AlreadyRunning running:
                logger.LogInformation(
                    "Задание уже выполняется. JobId={JobId}. Повторный захват запрещён, в том числе при истёкшей аренде. Сообщение оставлено без подтверждения.", running.JobId);
                return;

            case IngestionClaimResult.AlreadyCompleted completed:
                logger.LogInformation(
                    "Задание уже завершено успешно. JobId={JobId}. Повторная индексация не выполняется. Подтверждение сообщения пока не отправляется.", completed.JobId);
                return;

            case IngestionClaimResult.Terminal terminal:
                logger.LogInformation(
                    "Задание в конечном состоянии {Status}. JobId={JobId}. Сообщение оставлено без подтверждения.", terminal.Status, terminal.JobId);
                return;

            case IngestionClaimResult.RetryScheduled retry:
                logger.LogInformation(
                    "Задание ожидает повторной попытки. JobId={JobId}, NextAttemptAt={NextAttemptAt:O}. Запуск из RetryWait не выполняется. Сообщение оставлено без подтверждения.", retry.JobId, retry.NextAttemptAt);
                return;

            case IngestionClaimResult.Stale:
                logger.LogInformation(
                    "Сообщение не совпадает с заданием или документ удалён. JobId={JobId}, DocumentId={DocumentId}, ContentVersion={ContentVersion}, ProcessingVersion={ProcessingVersion}. Сообщение оставлено без подтверждения.",
                    message.JobId,
                    message.DocumentId,
                    message.ContentVersion,
                    message.ProcessingVersion);
                return;

            case IngestionClaimResult.NotFound:
                logger.LogInformation(
                    "Задание или документ не найдены. JobId={JobId}, DocumentId={DocumentId}. Сообщение оставлено без подтверждения.", message.JobId, message.DocumentId);
                return;

            case IngestionClaimResult.NotClaimable:
                logger.LogInformation(
                    "Задание в очереди, но документ нельзя перевести в обработку. JobId={JobId}, DocumentId={DocumentId}. Состояние не изменено. Сообщение оставлено без подтверждения.",
                    message.JobId,
                    message.DocumentId);
                return;

            default:
                throw new InvalidOperationException(
                    $"Результат по неподтвержденному заявлению о приеме: {result.GetType().Name}");
        }
    }
}