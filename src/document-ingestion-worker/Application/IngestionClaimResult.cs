namespace EnterpriseGenAI.DocumentIngestion.Worker.Application;

public abstract record IngestionClaimResult
{
    // Захват выполнен: задание в Running, выдан lease
    public sealed record Acquired(Guid JobId, Guid LeaseToken, DateTime LeaseUntil) : IngestionClaimResult;

    // Задание уже в Running; повторный захват запрещён
    public sealed record AlreadyRunning(Guid JobId) : IngestionClaimResult;

    // Задание уже Succeeded; повторная обработка не нужна
    public sealed record AlreadyCompleted(Guid JobId) : IngestionClaimResult;

    // Конечное состояние: Failed, Superseded или Cancelled
    public sealed record Terminal(Guid JobId, string Status) : IngestionClaimResult;

    // Задание в RetryWait; запуск из этого статуса запрещён
    public sealed record RetryScheduled(Guid JobId, DateTime? NextAttemptAt) : IngestionClaimResult;

    // Версии/идентификаторы не совпали или документ удалён
    public sealed record Stale(Guid JobId) : IngestionClaimResult;

    // Задание или документ отсутствуют
    public sealed record NotFound(Guid JobId) : IngestionClaimResult;

    // Job в Queued, но документ не готов к захвату
    public sealed record NotClaimable(Guid JobId) : IngestionClaimResult;
}