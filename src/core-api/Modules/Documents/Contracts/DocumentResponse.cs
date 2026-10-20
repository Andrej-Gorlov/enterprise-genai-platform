
using EnterpriseGenAI.Core.Api.Modules.Documents.Domain;

namespace EnterpriseGenAI.Core.Api.Modules.Documents.Contracts;

public sealed record DocumentResponse(
    Guid DocumentId,
    string FileName,
    string ContentType,
    long Size,
    string? Checksum,
    DocumentStatus Status,
    int ContentVersion,
    int ProcessingVersion,
    int? ReadyProcessingVersion,
    DateTime CreatedAt
);
