using System.Net;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using EnterpriseGenAI.Core.Api.Infrastructure.Ai;
using EnterpriseGenAI.Core.Api.Infrastructure.Ai.Contracts;

namespace EnterpriseGenAI.Core.Api.Tests.Infrastructure;

public class AiOrchestratorClientTests
{
    private static readonly Uri BaseTestAddress = new("http://ai-orchestrator.test/");

    private static HttpClient CreateHttpClient(HttpMessageHandler handler)
    {
        return new HttpClient(handler)
        {
            BaseAddress = BaseTestAddress
        };
    }

    [Fact]
    public async Task Similarity_MapsConnectionFailureToUnavailable()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromException<HttpResponseMessage>(new HttpRequestException("Connection refused")));

        using var httpClient = CreateHttpClient(handler);
        var aiClient = new AiOrchestratorClient(httpClient);

        var exception = await Assert.ThrowsAsync<AiOrchestratorUnavailableException>(() =>
            aiClient.GetSemanticSimilarityAsync(new AiSimilarityRequest("first", "second"), CancellationToken.None));

        Assert.IsType<HttpRequestException>(exception.InnerException, exactMatch: false);
    }

    [Fact]
    public async Task Similarity_MapsInternalCancellationToTimeout()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromException<HttpResponseMessage>(new OperationCanceledException("Upstream timeout")));

        using var httpClient = CreateHttpClient(handler);
        var aiClient = new AiOrchestratorClient(httpClient);

        await Assert.ThrowsAsync<AiOrchestratorTimeoutException>(() =>
            aiClient.GetSemanticSimilarityAsync(new AiSimilarityRequest("first", "second"), CancellationToken.None));
    }

    [Fact]
    public async Task Similarity_PreservesCallerCancellation()
    {
        using var cts = new CancellationTokenSource();
        var requestStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var handler = new StubHttpMessageHandler(async (_, token) =>
        {
            requestStarted.SetResult();
            await Task.Delay(Timeout.Infinite, token);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        using var httpClient = CreateHttpClient(handler);
        var aiClient = new AiOrchestratorClient(httpClient);

        var clientTask = aiClient.GetSemanticSimilarityAsync(new AiSimilarityRequest("first", "second"), cts.Token);

        await requestStarted.Task;
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => clientTask);
    }

    [Theory]
    [InlineData(422)]
    [InlineData(500)]
    public async Task Similarity_MapsUpstreamHttpErrorToResponseException(int statusCode)
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage((HttpStatusCode)statusCode)));

        using var httpClient = CreateHttpClient(handler);
        var aiClient = new AiOrchestratorClient(httpClient);

        await Assert.ThrowsAsync<AiOrchestratorResponseException>(() =>
            aiClient.GetSemanticSimilarityAsync(new AiSimilarityRequest("first", "second"), CancellationToken.None));
    }

    [Fact]
    public async Task Similarity_RejectsInvalidJson()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{invalid json", Encoding.UTF8, MediaTypeNames.Application.Json)
            }));

        using var httpClient = CreateHttpClient(handler);
        var aiClient = new AiOrchestratorClient(httpClient);

        var exception = await Assert.ThrowsAsync<AiOrchestratorResponseException>(() =>
            aiClient.GetSemanticSimilarityAsync(new AiSimilarityRequest("first", "second"), CancellationToken.None));

        Assert.IsType<JsonException>(exception.InnerException, exactMatch: false);
    }

    [Fact]
    public async Task Similarity_RejectsMissingScore()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, MediaTypeNames.Application.Json)
            }));

        using var httpClient = CreateHttpClient(handler);
        var aiClient = new AiOrchestratorClient(httpClient);

        var exception = await Assert.ThrowsAsync<AiOrchestratorResponseException>(() =>
            aiClient.GetSemanticSimilarityAsync(new AiSimilarityRequest("first", "second"), CancellationToken.None));

        Assert.IsType<JsonException>(exception.InnerException, exactMatch: false);
    }
}