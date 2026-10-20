
using System.ComponentModel.DataAnnotations;

namespace EnterpriseGenAI.Core.Api.Modules.Documents.Contracts;

public sealed record RegisterDocumentRequest(
    [property: Required]
    [property: StringLength(512, MinimumLength = 1)]
    string FileName,

    [property: Required]
    [property: StringLength(128, MinimumLength = 1)]
    string ContentType,

    [property: Range(1, long.MaxValue)]
    long Size
);
