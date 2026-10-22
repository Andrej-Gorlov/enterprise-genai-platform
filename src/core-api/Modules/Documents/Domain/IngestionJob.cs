namespace EnterpriseGenAI.Core.Api.Modules.Documents.Domain;

public sealed class IngestionJob
{
    // уникальный идентификатор задания
    public Guid JobId { get; private set; }

    // документ, к которому относится задание
    public Guid DocumentId { get; private set; }

    // версия содержимого документа
    public int ContentVersion { get; private set; }

    // версия выполняемой обработки
    public int ProcessingVersion { get; private set; }

    // идентификатор модели embeddings
    public string ModelId { get; private set; } = string.Empty;

    // ревизия модели embeddings
    public string ModelRevision { get; private set; } = string.Empty;

    // текущее состояние задания
    public IngestionJobStatus Status { get; private set; }

    // число попыток обработки
    public int AttemptCount { get; private set; }

    // время следующей попытки (для retry)
    public DateTime? NextAttemptAt { get; private set; }

    // срок аренды задания Worker
    public DateTime? LeaseUntil { get; private set; }

    // fencing-токен текущего захвата
    public Guid? LeaseToken { get; private set; }

    // код последней ошибки
    public string? LastErrorCode { get; private set; }

    // сквозной идентификатор для диагностики
    public string? CorrelationId { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private IngestionJob() { }

    // Создаёт задание в статусе Queued с нулевым числом попыток
    public static IngestionJob CreateQueued(
        Guid documentId,
        int contentVersion,
        int processingVersion,
        string modelId,
        string modelRevision,
        string? correlationId = null)
    {
        if (documentId == Guid.Empty)
            throw new ArgumentException("DocumentId не может быть пустым.", nameof(documentId));

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(contentVersion);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(processingVersion);

        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelRevision);

        modelId = modelId.Trim();
        modelRevision = modelRevision.Trim();

        ArgumentOutOfRangeException.ThrowIfGreaterThan(modelId.Length, 128, nameof(modelId));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(modelRevision.Length, 128, nameof(modelRevision));

        var normalizedCorrelationId = string.IsNullOrWhiteSpace(correlationId) ? null : correlationId.Trim();

        ArgumentOutOfRangeException.ThrowIfGreaterThan(normalizedCorrelationId?.Length ?? 0, 128, nameof(correlationId));

        var now = DateTime.UtcNow;

        return new IngestionJob
        {
            JobId = Guid.NewGuid(),
            DocumentId = documentId,
            ContentVersion = contentVersion,
            ProcessingVersion = processingVersion,
            ModelId = modelId.Trim(),
            ModelRevision = modelRevision.Trim(),
            Status = IngestionJobStatus.Queued,
            AttemptCount = 0,
            CorrelationId = normalizedCorrelationId,
            CreatedAt = now,
            UpdatedAt = now
        };
    }
}