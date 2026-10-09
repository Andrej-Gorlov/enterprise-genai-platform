using System.Net;
using EnterpriseGenAI.Core.Api.Infrastructure.Http;
using Microsoft.AspNetCore.Http;

namespace EnterpriseGenAI.Core.Api.Tests.Infrastructure;

public class CorrelationIdPropagationHandlerTests
{
    private const string CorrelationHeaderName = "X-Correlation-ID";
    private static readonly Uri TestEndpoint = new("http://ai-orchestrator.test/health");

    [Fact]
    public async Task SendAsync_ForwardsCorrelationId()
    {
        const string expectedId = "stage-26-5-outbound";

        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                TraceIdentifier = expectedId
            }
        };

        string? actualId = null;

        var transport = new StubHttpMessageHandler((request, _) =>
        {
            actualId = request.Headers.GetValues(CorrelationHeaderName).Single();

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        using var handler = new CorrelationIdPropagationHandler(accessor)
        {
            InnerHandler = transport
        };

        using var client = new HttpClient(handler);

        using var response = await client.GetAsync(TestEndpoint);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(expectedId, actualId);
    }

    [Fact]
    public async Task SendAsync_WithoutHttpContext_OmitsCorrelationId()
    {
        var accessor = new HttpContextAccessor
        {
            HttpContext = null
        };

        bool headerWasPresent = false;

        var transport = new StubHttpMessageHandler((request, _) =>
        {
            headerWasPresent = request.Headers.Contains(CorrelationHeaderName);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        using var handler = new CorrelationIdPropagationHandler(accessor)
        {
            InnerHandler = transport
        };

        using var client = new HttpClient(handler);

        using var response = await client.GetAsync(TestEndpoint);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(headerWasPresent, $"Header '{CorrelationHeaderName}' should not be added when HttpContext is null.");
    }
}