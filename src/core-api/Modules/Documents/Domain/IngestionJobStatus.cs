namespace EnterpriseGenAI.Core.Api.Modules.Documents.Domain;

public enum IngestionJobStatus
{
    Queued = 0,
    Running = 1,
    RetryWait = 2,
    Succeeded = 3,
    Failed = 4,
    Superseded = 5,
    Cancelled = 6
}