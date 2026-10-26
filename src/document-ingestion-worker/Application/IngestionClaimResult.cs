namespace EnterpriseGenAI.DocumentIngestion.Worker.Application;

public abstract record IngestionClaimResult
{
    public sealed record Acquired(Guid JobId, Guid LeaseToken, DateTime LeaseUntil) : IngestionClaimResult;

    public sealed record NotAcquired : IngestionClaimResult
    {
        public static readonly NotAcquired Instance = new();
    }
}