
using System.ComponentModel.DataAnnotations;

namespace EnterpriseGenAI.Core.Api.Modules.Documents.Infrastructure;

public sealed class DocumentStorageOptions
{
    public const string SectionName = "DocumentStorage";

    [Required]
    public string InternalEndpoint { get; init; } = string.Empty;

    [Required]
    public string PublicEndpoint { get; init; } = string.Empty;

    [Required]
    public string BucketName { get; init; } = string.Empty;

    [Required]
    public string AccessKey { get; init; } = string.Empty;

    [Required]
    public string SecretKey { get; init; } = string.Empty;

    [Range(1, 60)]
    public int PresignedUrlExpiryMinutes { get; init; } = 10;
}
