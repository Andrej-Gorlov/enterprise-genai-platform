using EnterpriseGenAI.Core.Api.Infrastructure.Ai;
using EnterpriseGenAI.Core.Api.Infrastructure.Ai.Contracts;

namespace EnterpriseGenAI.Core.Api.Tests.Infrastructure;

public sealed class FakeAiOrchestratorClient : IAiOrchestratorClient
{
    public int Calls { get; private set; }

    public AiSimilarityRequest? LastRequest { get; private set; }

    public Task<AiSimilarityResponse> GetSemanticSimilarityAsync(AiSimilarityRequest request, CancellationToken cancellationToken)
    {
        Calls++;
        LastRequest = request;
        return Task.FromResult(new AiSimilarityResponse(0.75));
    }
}