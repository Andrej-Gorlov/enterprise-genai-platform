namespace EnterpriseGenAI.Core.Api.Infrastructure.Messaging;

public interface IRabbitMqMessagePublisher
{
    Task PublishAsync(Guid messageId, string eventType, int schemaVersion, string payload, CancellationToken cancellationToken);
}