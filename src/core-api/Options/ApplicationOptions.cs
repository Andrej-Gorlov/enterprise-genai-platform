using System.ComponentModel.DataAnnotations;

namespace EnterpriseGenAI.Core.Api.Options;

public sealed class ApplicationOptions
{
    public const string SectionName = "Application";

    [Required]
    public string Name { get; set; } = string.Empty;
}