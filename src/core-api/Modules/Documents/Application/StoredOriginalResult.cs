namespace EnterpriseGenAI.Core.Api.Modules.Documents.Application;

public sealed record StoredOriginalResult(
    string ObjectKey,
    long Size,
    string Checksum
);