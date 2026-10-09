using EnterpriseGenAI.Core.Api.Infrastructure.Ai;
using EnterpriseGenAI.Core.Api.Infrastructure.Ai.Contracts;

namespace EnterpriseGenAI.Core.Api.Modules.Ai.Api;

public static class AiEndpoints
{
    public static IEndpointRouteBuilder MapAiEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/ai/semantic-similarity", async (
            SemanticSimilarityRequest request,
            IAiOrchestratorClient aiOrchestratorClient,
            CancellationToken cancellationToken) =>
        {
            var aiResponse = await aiOrchestratorClient.GetSemanticSimilarityAsync(new AiSimilarityRequest(request.Left, request.Right), cancellationToken);
            return Results.Ok(new SemanticSimilarityResponse(aiResponse.Score));
        });

        return endpoints;
    }
}