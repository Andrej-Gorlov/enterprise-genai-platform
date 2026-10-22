using System.Text;
using EnterpriseGenAI.Core.Api.Options;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace EnterpriseGenAI.Core.Api.Infrastructure.Messaging;

public sealed class RabbitMqMessagePublisher : IRabbitMqMessagePublisher, IAsyncDisposable
{
    private readonly RabbitMqOptions _options;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _sync = new();

    private IConnection? _connection;
    private IChannel? _channel;
    private bool _disposed;

    public RabbitMqMessagePublisher(IOptions<RabbitMqOptions> options)
    {
        _options = options.Value;
    }

    public async Task PublishAsync(Guid messageId, string eventType, int schemaVersion, string payload, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            var channel = await EnsureChannelAsync(cancellationToken).ConfigureAwait(false);
            await DeclareTopologyAsync(channel, cancellationToken).ConfigureAwait(false);

            var properties = new BasicProperties
            {
                MessageId = messageId.ToString("D"),
                Type = eventType,
                ContentType = "application/json",
                DeliveryMode = DeliveryModes.Persistent,
                Headers = new Dictionary<string, object?> { ["schema-version"] = schemaVersion }
            };

            await channel.BasicPublishAsync(
                exchange: RabbitMqTopology.Exchange,
                routingKey: RabbitMqTopology.RoutingKey,
                mandatory: true,
                basicProperties: properties,
                body: Encoding.UTF8.GetBytes(payload),
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await ResetAsync().ConfigureAwait(false);
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<IChannel> EnsureChannelAsync(CancellationToken ct)
    {
        if (_channel is { IsOpen: true } && _connection is { IsOpen: true })
            return _channel;

        await ResetAsync().ConfigureAwait(false);

        var factory = new ConnectionFactory
        {
            HostName = _options.HostName,
            Port = _options.Port,
            UserName = _options.UserName,
            Password = _options.Password,
            VirtualHost = _options.VirtualHost
        };

        var connection = await factory.CreateConnectionAsync(ct).ConfigureAwait(false);

        try
        {
            var channel = await connection.CreateChannelAsync(
                new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true), ct).ConfigureAwait(false);

            lock (_sync)
            {
                _connection = connection;
                _channel = channel;
            }

            return channel;
        }
        catch
        {
            try
            {
                await connection.DisposeAsync().ConfigureAwait(false);
            }
            catch
            {
                // Сохраняем исходную ошибку создания канала.
            }

            throw;
        }
    }

    private static async Task DeclareTopologyAsync(IChannel channel, CancellationToken ct)
    {
        await channel.ExchangeDeclareAsync(
            RabbitMqTopology.Exchange, ExchangeType.Direct,
            durable: true, autoDelete: false, cancellationToken: ct).ConfigureAwait(false);

        await channel.QueueDeclareAsync(
            RabbitMqTopology.Queue,
            durable: true, exclusive: false, autoDelete: false,
            cancellationToken: ct).ConfigureAwait(false);

        await channel.QueueBindAsync(
            RabbitMqTopology.Queue, RabbitMqTopology.Exchange,
            RabbitMqTopology.RoutingKey, cancellationToken: ct).ConfigureAwait(false);
    }

    private async Task ResetAsync()
    {
        IChannel? channel;
        IConnection? connection;

        lock (_sync)
        {
            channel = _channel;
            connection = _connection;
            _channel = null;
            _connection = null;
        }

        if (channel is not null)
            try { await channel.DisposeAsync().ConfigureAwait(false); } catch { /* ignore */ }

        if (connection is not null)
            try { await connection.DisposeAsync().ConfigureAwait(false); } catch { /* ignore */ }
    }

    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);

        try
        {
            if (_disposed)
                return;

            _disposed = true;

            await ResetAsync().ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }
}