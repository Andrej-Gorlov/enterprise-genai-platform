using System.ComponentModel.DataAnnotations;

namespace EnterpriseGenAI.DocumentIngestion.Worker.Options;

public sealed class IngestionLeaseOptions
{
    public const string SectionName = "IngestionLease";

    [Range(1, 3600)]
    public int LeaseSeconds { get; set; } = 120;
}