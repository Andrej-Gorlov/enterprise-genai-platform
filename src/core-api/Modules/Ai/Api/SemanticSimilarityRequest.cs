using System.ComponentModel.DataAnnotations;

namespace EnterpriseGenAI.Core.Api.Modules.Ai.Api;

public sealed record SemanticSimilarityRequest(
    [property: Required]
    [property: StringLength(1000, MinimumLength = 1)]
    string Left,

    [property: Required]
    [property: StringLength(1000, MinimumLength = 1)]
    string Right);