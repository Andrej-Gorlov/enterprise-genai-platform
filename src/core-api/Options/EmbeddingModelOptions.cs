using System.ComponentModel.DataAnnotations;

namespace EnterpriseGenAI.Core.Api.Options;

public sealed class EmbeddingModelOptions
{
    public const string SectionName = "EmbeddingModel";

    [Required]
    [MaxLength(128)]
    public string ModelId { get; set; } = string.Empty;

    [Required]
    [MaxLength(128)]
    public string ModelRevision { get; set; } = string.Empty;
}