namespace EnterpriseGenAI.Core.Api.Infrastructure.Ai;

public sealed class AiOrchestratorUnavailableException : Exception
{
    public AiOrchestratorUnavailableException(Exception innerException) : base("AI Orchestrator is unavailable.", innerException) {}
}