namespace EnterpriseGenAI.Core.Api.Infrastructure.Ai;

public sealed class AiOrchestratorTimeoutException : Exception
{
    public AiOrchestratorTimeoutException(Exception innerException) : base("AI Orchestrator request timed out.", innerException) {}
}