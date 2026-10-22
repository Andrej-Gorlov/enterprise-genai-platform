using System.Text.Json;

namespace EnterpriseGenAI.Core.Api.Infrastructure.Outbox;

public sealed class OutboxMessage
{
    public Guid MessageId { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public int SchemaVersion { get; private set; }
    public string Payload { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public DateTime? PublishedAt { get; private set; }

    private OutboxMessage() { }

    public static OutboxMessage Create(string eventType, int schemaVersion, string payload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(schemaVersion);

        eventType = eventType.Trim();

        ArgumentOutOfRangeException.ThrowIfGreaterThan( eventType.Length, 128, nameof(eventType));

        try
        {
            using var document = JsonDocument.Parse(payload);

            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException("Payload должен быть JSON-объектом.", nameof(payload));
            }
        }
        catch (JsonException ex)
        {
            throw new ArgumentException("Payload содержит некорректный JSON.", nameof(payload), ex);
        }

        return new OutboxMessage
        {
            MessageId = Guid.NewGuid(),
            EventType = eventType,
            SchemaVersion = schemaVersion,
            Payload = payload,
            CreatedAt = DateTime.UtcNow,
            PublishedAt = null
        };
    }

    public void MarkPublished()
    {
        if (PublishedAt is not null)
        {
            return;
        }
        PublishedAt = DateTime.UtcNow;
    }
}