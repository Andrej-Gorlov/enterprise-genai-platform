using EnterpriseGenAI.Core.Api.Infrastructure.Ai;
using EnterpriseGenAI.Core.Api.Infrastructure.Ai.Contracts;

namespace EnterpriseGenAI.Core.Api.Tests.Infrastructure;

public sealed class ThrowingAiOrchestratorClient(Exception exception) : IAiOrchestratorClient
{
    public Task<AiSimilarityResponse> GetSemanticSimilarityAsync(AiSimilarityRequest request, CancellationToken cancellationToken)
    {
        return Task.FromException<AiSimilarityResponse>(exception);
    }
}