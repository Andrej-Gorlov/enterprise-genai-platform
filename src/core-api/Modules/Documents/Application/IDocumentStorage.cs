namespace EnterpriseGenAI.Core.Api.Modules.Documents.Application;

public interface IDocumentStorage
{
    string CreatePresignedUploadUrl(string temporaryObjectKey, DateTime expiresAtUtc);

    Task<long?> GetTemporaryObjectSizeAsync(string temporaryObjectKey, CancellationToken cancellationToken);

    Task<StoredOriginalResult> StoreOriginalAsync(
        string temporaryObjectKey,
        Guid documentId,
        int contentVersion,
        long expectedSize,
        string expectedContentType,
        CancellationToken cancellationToken);
}