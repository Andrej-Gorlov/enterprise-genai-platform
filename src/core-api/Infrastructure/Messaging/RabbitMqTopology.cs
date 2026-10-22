namespace EnterpriseGenAI.Core.Api.Infrastructure.Messaging;

public static class RabbitMqTopology
{
    public const string Exchange = "documents.ingestion";
    public const string Queue = "documents.ingestion.jobs";
    public const string RoutingKey = "documents.ingestion.requested";
}