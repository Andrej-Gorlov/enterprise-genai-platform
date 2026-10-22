using EnterpriseGenAI.Core.Api.Infrastructure.Messaging;
using EnterpriseGenAI.Core.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseGenAI.Core.Api.Infrastructure.Outbox;

public sealed class OutboxPublisherBackgroundService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan PublishTimeout = TimeSpan.FromSeconds(15);
    private const int BatchSize = 25;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IRabbitMqMessagePublisher _publisher;
    private readonly ILogger<OutboxPublisherBackgroundService> _logger;

    public OutboxPublisherBackgroundService(IServiceScopeFactory scopeFactory, IRabbitMqMessagePublisher publisher, ILogger<OutboxPublisherBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _publisher = publisher;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Outbox publisher started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Цикл работы издателя исходящих сообщений завершился с ошибкой.");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("Outbox publisher stopped.");
    }

    private async Task ProcessBatchAsync(CancellationToken stoppingToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var messages = await db.OutboxMessages
            .Where(m => m.PublishedAt == null)
            .OrderBy(m => m.CreatedAt)
            .ThenBy(m => m.MessageId)
            .Take(BatchSize)
            .ToListAsync(stoppingToken)
            .ConfigureAwait(false);

        if (messages.Count == 0)
        {
            return;
        }

        foreach (var message in messages)
        {
            try
            {
                using var publishCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                publishCts.CancelAfter(PublishTimeout);

                await _publisher.PublishAsync(
                    message.MessageId,
                    message.EventType,
                    message.SchemaVersion,
                    message.Payload,
                    publishCts.Token).ConfigureAwait(false);

                message.MarkPublished();
                await db.SaveChangesAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish outbox message {MessageId} of type {EventType}.", message.MessageId, message.EventType);

                // После ошибки publish/SaveChanges не продолжаем с тем же DbContext.
                return;
            }
        }
    }
}