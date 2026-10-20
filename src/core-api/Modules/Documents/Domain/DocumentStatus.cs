namespace EnterpriseGenAI.Core.Api.Modules.Documents.Domain;

public enum DocumentStatus
{
    PendingUpload = 0,
    Uploaded = 1,
    Queued = 2,
    Processing = 3,
    Ready = 4,
    Failed = 5,
    Deleting = 6,
    Deleted = 7
}