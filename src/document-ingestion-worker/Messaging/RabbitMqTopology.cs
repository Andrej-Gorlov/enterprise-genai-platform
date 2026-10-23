namespace EnterpriseGenAI.DocumentIngestion.Worker.Messaging;

public static class RabbitMqTopology
{
    // Имя exchange, в который Core API публикует задания ingestion
    public const string ExchangeName = "documents.ingestion";

    // Routing key привязки сообщения к очереди ingestion
    public const string RoutingKey = "documents.ingestion.requested";

    // Очередь, из которой Worker потребляет задания (autoAck: false)
    public const string QueueName = "documents.ingestion.jobs";


    // Значение BasicProperties.Type для контракта DocumentIngestionRequested
    public const string MessageType = "DocumentIngestionRequested";

    // Имя заголовка с версией схемы тела сообщения
    public const string SchemaVersionHeader = "schema-version";

    // Ожидаемая версия схемы JSON-контракта
    public const string SchemaVersion = "1";

    // Ожидаемый Content-Type тела сообщения
    public const string ContentType = "application/json";
}