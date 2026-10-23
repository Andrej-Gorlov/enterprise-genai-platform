namespace EnterpriseGenAI.DocumentIngestion.Worker.Contracts;

public sealed record DocumentIngestionRequested(
    Guid JobId,
    Guid DocumentId,
    int ContentVersion,
    int ProcessingVersion);