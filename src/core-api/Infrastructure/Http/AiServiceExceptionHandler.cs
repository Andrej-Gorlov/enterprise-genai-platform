using EnterpriseGenAI.Core.Api.Infrastructure.Ai;
using Microsoft.AspNetCore.Diagnostics;

namespace EnterpriseGenAI.Core.Api.Infrastructure.Http;

public sealed class AiServiceExceptionHandler(ILogger<AiServiceExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var problem = exception switch
        {
            AiOrchestratorUnavailableException => (
                Status: 503,
                Title: "AI service unavailable",
                Detail: "The AI service is temporarily unavailable."),

            AiOrchestratorTimeoutException => (
                Status: 504,
                Title: "AI service timeout",
                Detail: "The AI service did not respond in time."),

            AiOrchestratorResponseException => (
                Status: 502,
                Title: "AI service error",
                Detail: "The AI service returned an invalid response."),

            _ => (Status: 0, Title: "", Detail: "")
        };

        if (problem.Status == 0)
        {
            return false;
        }

        logger.LogWarning(
            "AI integration failure. StatusCode: {StatusCode}, ExceptionType: {ExceptionType}, CorrelationId: {CorrelationId}",
            problem.Status,
            exception.GetType().Name,
            httpContext.TraceIdentifier);

        await Results.Problem(
            statusCode: problem.Status,
            title: problem.Title,
            detail: problem.Detail)
            .ExecuteAsync(httpContext);

        return true;
    }
}