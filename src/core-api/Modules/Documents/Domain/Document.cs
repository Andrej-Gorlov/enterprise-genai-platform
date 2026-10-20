
namespace EnterpriseGenAI.Core.Api.Modules.Documents.Domain;

public sealed class Document
{
    // уникальный идентификатор документа
    public Guid DocumentId { get; private set; }

    // идентификатор владельца документа
    public string OwnerId { get; private set; } = string.Empty;

    // исходное имя файла
    public string FileName { get; private set; } = string.Empty;

    // проверенный MIME-тип файла
    public string ContentType { get; private set; } = string.Empty;

    // размер файла в байтах
    public long Size { get; private set; }

    public string TemporaryObjectKey { get; private set; } = string.Empty;
    
    // ключ оригинала в MinIO
    public string? ObjectKey { get; private set; }

    // контрольная сумма содержимого
    public string? Checksum { get; private set; }

    #region Версионирование
    // версия содержимого документа
    public int ContentVersion { get; private set; } = 1;

    // версия текущей обработки
    public int ProcessingVersion { get; private set; }

    // версия завершённой индексации
    public int? ReadyProcessingVersion { get; private set; }
    #endregion 

    // текущее состояние документа
    public DocumentStatus Status { get; private set; }

    #region Дата
    public DateTime UploadExpiresAtUtc { get; private set; }

    // дата и время создания
    public DateTime CreatedAt { get; private set; }

    // дата и время последнего изменения
    public DateTime UpdatedAt { get; private set; }

    // дата и время логического удаления
    public DateTime? DeletedAt { get; private set; }
    #endregion 

    private Document() { }

    // Создает новый документ в статусе PendingUpload с первой версией содержимого и начальными метаданными
    public static Document CreatePending(string ownerId, string fileName, string contentType, long size, string temporaryObjectKey, DateTime uploadExpiresAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentException.ThrowIfNullOrWhiteSpace(temporaryObjectKey);

        if (size <= 0)
            throw new ArgumentOutOfRangeException(nameof(size));

        var now = DateTime.UtcNow;

        if (uploadExpiresAtUtc.Kind != DateTimeKind.Utc || uploadExpiresAtUtc <= now)
        {
            throw new ArgumentException("Срок действия загруженного файла должен истекать в будущем (по времени UTC).", nameof(uploadExpiresAtUtc));
        }

        return new Document
        {
            DocumentId = Guid.NewGuid(),
            OwnerId = ownerId,
            FileName = fileName,
            ContentType = contentType,
            Size = size,
            TemporaryObjectKey = temporaryObjectKey,
            ContentVersion = 1,
            ProcessingVersion = 0,
            Status = DocumentStatus.PendingUpload,
            UploadExpiresAtUtc = uploadExpiresAtUtc,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    // Подтверждает успешную загрузку оригинала в хранилище, сохраняет контрольную сумму и переводит в статус Uploaded
    public void ConfirmUpload(string objectKey, string checksum)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(checksum);

        if (checksum.Length != 64 || !checksum.All(Uri.IsHexDigit))
            throw new ArgumentException("Контрольная сумма должна представлять собой шестнадцатеричную строку SHA-256.", nameof(checksum));

        checksum = checksum.ToLowerInvariant();

        if (Status == DocumentStatus.Uploaded)
        {
            if (ObjectKey == objectKey && Checksum == checksum)
                return;

            throw new InvalidOperationException("Загрузка документа уже подтверждена.");
        }

        if (Status != DocumentStatus.PendingUpload)
            throw new InvalidOperationException("Документ не ожидает загрузки.");

        if (string.Equals(objectKey, TemporaryObjectKey, StringComparison.Ordinal))
        {
            throw new ArgumentException("Ключ подтвержденного объекта должен отличаться от временного ключа.", nameof(objectKey));
        }

        ObjectKey = objectKey;
        Checksum = checksum;
        Status = DocumentStatus.Uploaded;
        UpdatedAt = DateTime.UtcNow;
    }

    // Фиксирует сбой загрузки или обработки и переводит документ в терминальный статус Failed
    public void MarkUploadFailed()
    {
        if (Status == DocumentStatus.Failed)
            return;

        if (Status != DocumentStatus.PendingUpload)
            throw new InvalidOperationException("Только ожидающие загрузки можно пометить как неудавшиеся.");

        Status = DocumentStatus.Failed;
        UpdatedAt = DateTime.UtcNow;
    }
}
