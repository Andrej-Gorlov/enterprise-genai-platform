
using EnterpriseGenAI.Core.Api.Modules.Documents.Domain;

namespace EnterpriseGenAI.Core.Api.Modules.Documents.Contracts;

public sealed record RegisterDocumentResponse(
    Guid DocumentId,
    DocumentStatus Status,
    int ContentVersion,
    DateTime UploadExpiresAtUtc,
    string UploadUrl
);
