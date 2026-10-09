namespace EnterpriseGenAI.Core.Api.Infrastructure.Http;

public sealed class CorrelationIdPropagationHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    private const string HeaderName = "X-Correlation-ID";

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var correlationId = httpContextAccessor.HttpContext?.TraceIdentifier;

        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            request.Headers.Add(HeaderName, correlationId);
        }
        return base.SendAsync(request, cancellationToken);
    }
}