using System.Text;
using System.Text.Json;
using EnterpriseGenAI.DocumentIngestion.Worker.Contracts;
using RabbitMQ.Client;

namespace EnterpriseGenAI.DocumentIngestion.Worker.Messaging;

public static class IngestionMessageValidator
{
    public const int MaxBodyBytes = 64 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static bool TryValidate(IReadOnlyBasicProperties props, ReadOnlyMemory<byte> body, out DocumentIngestionRequested? message, out string error)
    {
        message = null;

        error = (body.Length, props.Type, props.ContentType, props.MessageId, props.Headers) switch
        {
            (0 or > MaxBodyBytes, _, _, _, _) => "Недопустимый размер сообщения.",
            (_, not RabbitMqTopology.MessageType, _, _, _) => "Неподдерживаемый тип сообщения.",
            (_, _, var contentType, _, _) when
                !string.Equals(contentType, RabbitMqTopology.ContentType, StringComparison.OrdinalIgnoreCase) => "Неподдерживаемый тип контента.",
            (_, _, _, var id, _) when
                !Guid.TryParseExact(id, "D", out var messageId) || messageId == Guid.Empty => "Недопустимый MessageId.",
            (_, _, _, _, var headers) when
                headers is null 
                || !headers.TryGetValue(RabbitMqTopology.SchemaVersionHeader, out var version) 
                || DecodeHeader(version) != RabbitMqTopology.SchemaVersion => "Неподдерживаемая версия схемы.",
            _ => string.Empty
        };

        if (error.Length > 0) return false;

        try
        {
            message = JsonSerializer.Deserialize<DocumentIngestionRequested>(body.Span, JsonOptions);
        }
        catch (JsonException)
        {
            error = "Invalid JSON body.";
            return false;
        }

        error = message switch
        {
            null => "Пустое сообщение.",
            { JobId: var id } when id == Guid.Empty => "Неверный JobId.",
            { DocumentId: var id } when id == Guid.Empty => "Недопустимый DocumentId.",
            { ContentVersion: <= 0 } => "Недопустимый ContentVersion.",
            { ProcessingVersion: <= 0 } => "Недопустимая версия обработки (ProcessingVersion).",
            _ => string.Empty
        };

        if (error.Length == 0) return true;

        message = null;
        return false;
    }

    private static string DecodeHeader(object? value) => value switch
    {
        byte[] bytes => Encoding.UTF8.GetString(bytes),
        ReadOnlyMemory<byte> mem => Encoding.UTF8.GetString(mem.Span),
        string s => s,
        _ => value?.ToString() ?? string.Empty
    };
}