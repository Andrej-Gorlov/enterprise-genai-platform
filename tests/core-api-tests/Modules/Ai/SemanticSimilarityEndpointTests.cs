using System.Net;
using System.Net.Http.Json;
using EnterpriseGenAI.Core.Api.Infrastructure.Ai;
using EnterpriseGenAI.Core.Api.Modules.Ai.Api;
using EnterpriseGenAI.Core.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EnterpriseGenAI.Core.Api.Tests.Modules.Ai;

public sealed class SemanticSimilarityEndpointTests
{
    private static (HttpClient Client, FakeAiOrchestratorClient Fake) CreateClientWithFake(CoreApiFactory baseFactory)
    {
        var fake = new FakeAiOrchestratorClient();

        var client = baseFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IAiOrchestratorClient>();
                services.AddSingleton<IAiOrchestratorClient>(fake);
            });
        }).CreateClient();

        return (client, fake);
    }

    [Fact]
    public async Task SemanticSimilarity_ReturnsScore()
    {
        using var baseFactory = new CoreApiFactory();
        var (client, fake) = CreateClientWithFake(baseFactory);

        var request = new SemanticSimilarityRequest(
            Left: "Enterprise GenAI Platform",
            Right: "Enterprise AI Platform");

        var response = await client.PostAsJsonAsync("/ai/semantic-similarity", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("X-Correlation-ID", out var correlationIds));
        Assert.True(Guid.TryParse(Assert.Single(correlationIds), out _));

        var body = await response.Content.ReadFromJsonAsync<SemanticSimilarityResponse>();
        Assert.NotNull(body);
        Assert.Equal(0.75, body.Score);

        Assert.Equal(1, fake.Calls);
        Assert.NotNull(fake.LastRequest);
        Assert.Equal(request.Left, fake.LastRequest.Left);
        Assert.Equal(request.Right, fake.LastRequest.Right);
    }

    [Fact]
    public async Task SemanticSimilarity_RejectsBlankText()
    {
        using var baseFactory = new CoreApiFactory();
        var (client, fake) = CreateClientWithFake(baseFactory);

        var request = new SemanticSimilarityRequest(
            Left: "   ",
            Right: "Valid text");

        var response = await client.PostAsJsonAsync("/ai/semantic-similarity", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, fake.Calls);
        Assert.Null(fake.LastRequest);
    }

    [Fact]
    public async Task SemanticSimilarity_RejectsTooLongText()
    {
        using var baseFactory = new CoreApiFactory();
        var (client, fake) = CreateClientWithFake(baseFactory);

        var request = new SemanticSimilarityRequest(
            Left: new string('a', 1001),
            Right: "Valid text");

        var response = await client.PostAsJsonAsync("/ai/semantic-similarity", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, fake.Calls);
        Assert.Null(fake.LastRequest);
    }
}