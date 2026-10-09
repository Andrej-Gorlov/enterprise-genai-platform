namespace EnterpriseGenAI.Core.Api.Infrastructure.Ai;

public sealed class AiOrchestratorResponseException : Exception
{
    public AiOrchestratorResponseException(string message) : base(message) { }

    public AiOrchestratorResponseException(string message, Exception innerException) : base(message, innerException) { }
}