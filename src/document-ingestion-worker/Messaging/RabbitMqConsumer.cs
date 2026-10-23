using EnterpriseGenAI.DocumentIngestion.Worker.Application;
using EnterpriseGenAI.DocumentIngestion.Worker.Options;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace EnterpriseGenAI.DocumentIngestion.Worker.Messaging;

public sealed class RabbitMqConsumer(
    IOptions<RabbitMqOptions> options,
    IIngestionMessageHandler handler,
    IHostApplicationLifetime applicationLifetime,
    ILogger<RabbitMqConsumer> logger) : IAsyncDisposable
{
    private readonly RabbitMqOptions _options = options.Value;

    private IConnection? _connection;
    private IChannel? _channel;

    private CancellationToken _stoppingToken;

    private bool _consuming;

    public bool IsConsuming => _consuming;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Сохраняем токен остановки Generic Host.
        _stoppingToken = cancellationToken;

        var factory = new ConnectionFactory
        {
            HostName = _options.HostName,
            Port = _options.Port,
            UserName = _options.UserName,
            Password = _options.Password,
            VirtualHost = _options.VirtualHost
        };

        try
        {
            _connection = await factory.CreateConnectionAsync(cancellationToken);
            _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);

            await _channel.BasicQosAsync(
                prefetchSize: 0,
                prefetchCount: 1,
                global: false,
                cancellationToken: cancellationToken);

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += OnReceivedAsync;

            await _channel.BasicConsumeAsync(
                queue: RabbitMqTopology.QueueName,
                autoAck: false,
                consumer: consumer,
                cancellationToken: cancellationToken);

            _consuming = true;

            logger.LogInformation("RabbitMQ consumer запущен. Queue={Queue}, Prefetch=1, AutoAck=false", RabbitMqTopology.QueueName);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Не удалось запустить потребителя RabbitMQ. Host={Host}, Port={Port}", _options.HostName, _options.Port);
            throw;
        }
    }

    private async Task OnReceivedAsync(object sender, BasicDeliverEventArgs ea)
    {
        try
        {
            // Во время остановки новые сообщения не обрабатываем.
            if (_stoppingToken.IsCancellationRequested)
                return;

            // Копируем тело до завершения RabbitMQ callback.
            var bodyCopy = ea.Body.ToArray();

            if (!IngestionMessageValidator.TryValidate(ea.BasicProperties, bodyCopy, out var message, out var error))
            {
                logger.LogError("Некорректное сообщение при получении.  DeliveryTag={DeliveryTag}, Reason={Reason}. Сообщение оставлено без подтверждения (unacked).", ea.DeliveryTag, error);

                // Пока нет DLQ, не удаляем и не зацикливаем сообщение.
                return;
            }

            // Передаём CancellationToken от Generic Host.
            await handler.HandleAsync(message!, _stoppingToken);
            // BasicAckAsync намеренно НЕ вызываем.
        }
        catch (OperationCanceledException) when (_stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("Обработчик входящих данных был отменен во время завершения работы воркера.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Сбой в обработчике входящих сообщений. DeliveryTag={DeliveryTag}. Сообщение не подтверждено.", ea.DeliveryTag);

            // Ошибка не должна остаться незамеченной.
            // Generic Host инициирует остановку.
            applicationLifetime.StopApplication();
        }
    }

    public async ValueTask DisposeAsync()
    {
        _consuming = false;

        await DisposeSafelyAsync(_channel, "канала");
        _channel = null;

        await DisposeSafelyAsync(_connection, "соединения");
        _connection = null;
    }

    private async Task DisposeSafelyAsync(IAsyncDisposable? resource, string name)
    {
        if (resource is null) return;

        try
        {
            await resource.DisposeAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Ошибка при освобождении {Resource} RabbitMQ.", name);
        }
    }
}