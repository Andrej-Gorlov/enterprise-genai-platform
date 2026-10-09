using EnterpriseGenAI.Core.Api.Infrastructure.Ai.Contracts;

namespace EnterpriseGenAI.Core.Api.Infrastructure.Ai;

public interface IAiOrchestratorClient
{
    Task<AiSimilarityResponse> GetSemanticSimilarityAsync(AiSimilarityRequest request, CancellationToken cancellationToken);
}