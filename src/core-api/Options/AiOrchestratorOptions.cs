using System.ComponentModel.DataAnnotations;

namespace EnterpriseGenAI.Core.Api.Options;

public sealed class AiOrchestratorOptions
{
    public const string SectionName = "AiOrchestrator";

    [Required]
    public string BaseUrl { get; set; } = string.Empty;

    [Range(1, 300)]
    public int TimeoutSeconds { get; set; } = 30;
}