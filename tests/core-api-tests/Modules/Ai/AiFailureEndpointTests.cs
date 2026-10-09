using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EnterpriseGenAI.Core.Api.Infrastructure.Ai;
using EnterpriseGenAI.Core.Api.Modules.Ai.Api;
using EnterpriseGenAI.Core.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EnterpriseGenAI.Core.Api.Tests.Modules.Ai;

public class AiFailureEndpointTests : IClassFixture<CoreApiFactory>
{
    private const string CorrelationHeaderName = "X-Correlation-ID";
    private const string TestCorrelationId = "stage-26-5-failure-test";
    private const string EndpointUri = "/ai/semantic-similarity";

    private readonly CoreApiFactory _factory;

    public AiFailureEndpointTests(CoreApiFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData(502)]
    [InlineData(503)]
    [InlineData(504)]
    public async Task SemanticSimilarity_MapsAiFailuresToProblemDetails(int expectedStatus)
    {
        Exception exception = expectedStatus switch
        {
            502 => new AiOrchestratorResponseException("Internal downstream error details"),
            503 => new AiOrchestratorUnavailableException(new HttpRequestException("Internal connection details")),
            504 => new AiOrchestratorTimeoutException(new TaskCanceledException("Internal timeout details")),
            _ => throw new ArgumentOutOfRangeException(nameof(expectedStatus))
        };

        using var testFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IAiOrchestratorClient>();
                services.AddSingleton<IAiOrchestratorClient>(new ThrowingAiOrchestratorClient(exception));
            });
        });

        using var client = testFactory.CreateClient();

        var payload = new SemanticSimilarityRequest(
            Left: "Enterprise GenAI Platform",
            Right: "AI Platform"
        );

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, EndpointUri)
        {
            Content = JsonContent.Create(payload)
        };
        httpRequest.Headers.Add(CorrelationHeaderName, TestCorrelationId);

        using var response = await client.SendAsync(httpRequest);

        Assert.Equal((HttpStatusCode)expectedStatus, response.StatusCode);

        Assert.NotNull(response.Content.Headers.ContentType);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType.MediaType);

        Assert.True(
            response.Headers.TryGetValues(CorrelationHeaderName, out var correlationValues),
            $"Header '{CorrelationHeaderName}' was missing from the response."
        );
        Assert.Contains(TestCorrelationId, correlationValues);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.NotNull(problem);
        Assert.Equal(expectedStatus, problem.Status);
        Assert.False(string.IsNullOrWhiteSpace(problem.Title), "ProblemDetails.Title must not be empty.");
        Assert.False(string.IsNullOrWhiteSpace(problem.Detail), "ProblemDetails.Detail must not be empty.");

        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Internal", responseBody, StringComparison.OrdinalIgnoreCase);
    }
}