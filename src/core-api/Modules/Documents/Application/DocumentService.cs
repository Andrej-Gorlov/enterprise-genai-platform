
using EnterpriseGenAI.Core.Api.Infrastructure.Persistence;
using EnterpriseGenAI.Core.Api.Modules.Documents.Contracts;
using EnterpriseGenAI.Core.Api.Modules.Documents.Domain;
using EnterpriseGenAI.Core.Api.Modules.Documents.Infrastructure;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseGenAI.Core.Api.Modules.Documents.Application;

public sealed class DocumentService : IDocumentService
{
    private const long MaxFileSizeBytes = 20L * 1024 * 1024;

    private readonly AppDbContext _appDb;
    private readonly IDocumentStorage _storage;
    private readonly DocumentStorageOptions _options;

    public DocumentService(
        AppDbContext appDb,
        IDocumentStorage storage,
        IOptions<DocumentStorageOptions> options)
    {
        _appDb = appDb ?? throw new ArgumentNullException(nameof(appDb));
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<DocumentResponse> ConfirmUploadAsync(Guid documentId, string trustedOwnerId, CancellationToken cancellationToken)
    {
        if (documentId == Guid.Empty)
        {
            throw new ArgumentException("Идентификатор документа не может быть пустым.", nameof(documentId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(trustedOwnerId);
        if (trustedOwnerId.Length > 256 || trustedOwnerId.Any(char.IsControl))
        {
            throw new ArgumentException("Некорректный идентификатор владельца.", nameof(trustedOwnerId));
        }

        var document = await _appDb.Documents.FirstOrDefaultAsync(d => d.DocumentId == documentId && d.OwnerId == trustedOwnerId, cancellationToken);

        if (document is null)
        {
            throw new KeyNotFoundException($"Документ с идентификатором '{documentId}' не найден.");
        }

        if (document.Status == DocumentStatus.Uploaded)
        {
            return new DocumentResponse(
                document.DocumentId,
                document.FileName,
                document.ContentType,
                document.Size,
                document.Checksum,
                document.Status,
                document.ContentVersion,
                document.ProcessingVersion,
                document.ReadyProcessingVersion,
                document.CreatedAt
            );
        }

        if (document.Status != DocumentStatus.PendingUpload)
        {
            throw new InvalidOperationException($"Недопустимое состояние документа для подтверждения: {document.Status}.");
        }

        var result = await _storage.StoreOriginalAsync(
            document.TemporaryObjectKey,
            document.DocumentId,
            document.ContentVersion,
            document.Size,
            document.ContentType,
            cancellationToken);

        document.ConfirmUpload(result.ObjectKey, result.Checksum);

        await _appDb.SaveChangesAsync(cancellationToken);

        return new DocumentResponse(
            document.DocumentId,
            document.FileName,
            document.ContentType,
            document.Size,
            document.Checksum,
            document.Status,
            document.ContentVersion,
            document.ProcessingVersion,
            document.ReadyProcessingVersion,
            document.CreatedAt
        );
    }

    public async Task<RegisterDocumentResponse> RegisterUploadAsync(RegisterDocumentRequest request, string trustedOwnerId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(trustedOwnerId);

        if (trustedOwnerId.Length > 256 || trustedOwnerId.Any(char.IsControl))
        {
            throw new ArgumentException("Некорректный идентификатор владельца.", nameof(trustedOwnerId));
        }

        if (string.IsNullOrWhiteSpace(request.FileName) || request.FileName.Length > 512 || request.FileName.Any(c => char.IsControl(c) || c is '/' or '\\'))
        {
            throw new ArgumentException("Некорректное имя файла.", nameof(request));
        }

        var extension = Path.GetExtension(request.FileName).ToLowerInvariant();

        var expectedContentType = extension switch
        {
            ".pdf" => "application/pdf",
            ".txt" => "text/plain",
            ".md" => "text/markdown",
            _ => throw new ArgumentException("Неподдерживаемый формат документа.", nameof(request))
        };

        if (!string.Equals(request.ContentType, expectedContentType, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("MIME-тип не соответствует расширению файла.", nameof(request));
        }

        if (request.Size <= 0 || request.Size > MaxFileSizeBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Размер документа должен быть от 1 байта до 20 MiB.");
        }

        var temporaryObjectKey = $"uploads/{Guid.NewGuid():N}";

        var expiresAtUtc = DateTime.UtcNow.AddMinutes(_options.PresignedUrlExpiryMinutes);

        var document = Document.CreatePending(
            trustedOwnerId,
            request.FileName,
            expectedContentType,
            request.Size,
            temporaryObjectKey,
            expiresAtUtc);

        var uploadUrl = _storage.CreatePresignedUploadUrl(document.TemporaryObjectKey, document.UploadExpiresAtUtc);

        _appDb.Documents.Add(document);

        await _appDb.SaveChangesAsync(cancellationToken);

        return new RegisterDocumentResponse(
            document.DocumentId,
            document.Status,
            document.ContentVersion,
            document.UploadExpiresAtUtc,
            uploadUrl);
    }
}
