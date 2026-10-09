using System.Text.Json;
using EnterpriseGenAI.Core.Api.Infrastructure.Ai.Contracts;

namespace EnterpriseGenAI.Core.Api.Infrastructure.Ai;

public sealed class AiOrchestratorClient(HttpClient httpClient) : IAiOrchestratorClient
{
    public async Task<AiSimilarityResponse> GetSemanticSimilarityAsync(AiSimilarityRequest request, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;

        try
        {
            response = await httpClient.PostAsJsonAsync(
                "semantic-similarity",
                request,
                cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new AiOrchestratorUnavailableException(ex);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AiOrchestratorTimeoutException(ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
                throw new AiOrchestratorResponseException($"AI Orchestrator returned HTTP {(int)response.StatusCode}.");

            try
            {
                return await response.Content.ReadFromJsonAsync<AiSimilarityResponse>(cancellationToken)
                    ?? throw new AiOrchestratorResponseException("AI Orchestrator returned an empty response.");
            }
            catch (JsonException ex)
            {
                throw new AiOrchestratorResponseException("AI Orchestrator returned invalid JSON.", ex);
            }
        }
    }
}