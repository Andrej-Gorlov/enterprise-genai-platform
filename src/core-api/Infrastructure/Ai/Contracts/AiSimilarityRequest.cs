namespace EnterpriseGenAI.Core.Api.Infrastructure.Ai.Contracts;

public sealed record AiSimilarityRequest(
    string Left,
    string Right);