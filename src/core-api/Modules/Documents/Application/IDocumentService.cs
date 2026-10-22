
using EnterpriseGenAI.Core.Api.Modules.Documents.Contracts;

namespace EnterpriseGenAI.Core.Api.Modules.Documents.Application;

public interface IDocumentService
{
    Task<RegisterDocumentResponse> RegisterUploadAsync(RegisterDocumentRequest request, string trustedOwnerId, CancellationToken cancellationToken);
    
    Task<DocumentResponse> ConfirmUploadAsync(Guid documentId, string trustedOwnerId, CancellationToken cancellationToken);

    Task QueueForIngestionAsync(Guid documentId, string trustedOwnerId, string? correlationId, CancellationToken cancellationToken);
}
