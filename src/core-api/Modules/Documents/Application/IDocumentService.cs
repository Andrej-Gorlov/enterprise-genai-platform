
using EnterpriseGenAI.Core.Api.Modules.Documents.Contracts;

namespace EnterpriseGenAI.Core.Api.Modules.Documents.Application;

public interface IDocumentService
{
    Task<RegisterDocumentResponse> RegisterUploadAsync(RegisterDocumentRequest request, string trustedOwnerId, CancellationToken cancellationToken);
}
