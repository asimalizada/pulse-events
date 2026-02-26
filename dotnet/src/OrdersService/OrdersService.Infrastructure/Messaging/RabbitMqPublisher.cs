using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OrdersService.Domain;
using OrdersService.Domain.Contracts;
using OrdersService.Infrastructure.Persistence.Entities;
using RabbitMQ.Client;

namespace OrdersService.Infrastructure.Messaging;

public sealed class RabbitMqPublisher(ILogger<RabbitMqPublisher> logger) : IRabbitMqPublisher, IAsyncDisposable
{
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private readonly ConnectionFactory _connectionFactory = new()
    {
        HostName = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "localhost",
        UserName = Environment.GetEnvironmentVariable("RABBITMQ_USERNAME") ?? "guest",
        Password = Environment.GetEnvironmentVariable("RABBITMQ_PASSWORD") ?? "guest",
        AutomaticRecoveryEnabled = true,
        NetworkRecoveryInterval = TimeSpan.FromSeconds(5),
    };

    private IConnection? _connection;
    private IChannel? _channel;

    public async Task PublishOrderCreatedAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        var channel = await EnsureConnectedAsync(cancellationToken);
        var integrationEvent = JsonSerializer.Deserialize<OrderCreatedIntegrationEvent>(message.Payload)
            ?? throw new InvalidOperationException("Outbox payload could not be deserialized.");
        var body = Encoding.UTF8.GetBytes(message.Payload);

        var properties = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json",
            Type = message.Type,
            MessageId = message.EventId.ToString(),
            Headers = new Dictionary<string, object?>
            {
                [MessagingConstants.CorrelationHeader] = integrationEvent.CorrelationId.ToString(),
                [MessagingConstants.RetryCountHeader] = 0,
            },
        };

        await channel.BasicPublishAsync(
            exchange: MessagingConstants.ExchangeName,
            routingKey: MessagingConstants.RoutingKey,
            mandatory: true,
            basicProperties: properties,
            body: body,
            cancellationToken: cancellationToken);
    }

    public async Task<bool> CanConnectAsync(CancellationToken cancellationToken)
    {
        try
        {
            var channel = await EnsureConnectedAsync(cancellationToken);
            return channel.IsOpen;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "RabbitMQ connectivity check failed.");
            return false;
        }
    }

    private async Task<IChannel> EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (_connection?.IsOpen == true && _channel?.IsOpen == true)
        {
            return _channel;
        }

        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            if (_connection?.IsOpen == true && _channel?.IsOpen == true)
            {
                return _channel;
            }

            if (_channel is not null)
            {
                await _channel.DisposeAsync();
                _channel = null;
            }

            if (_connection is not null)
            {
                await _connection.DisposeAsync();
                _connection = null;
            }

            _connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
            _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);

            await _channel.ExchangeDeclareAsync(
                exchange: MessagingConstants.ExchangeName,
                type: ExchangeType.Topic,
                durable: true,
                autoDelete: false,
                arguments: null,
                passive: false,
                noWait: false,
                cancellationToken: cancellationToken);

            await _channel.QueueDeclareAsync(
                queue: MessagingConstants.QueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null,
                passive: false,
                noWait: false,
                cancellationToken: cancellationToken);

            await _channel.QueueBindAsync(
                queue: MessagingConstants.QueueName,
                exchange: MessagingConstants.ExchangeName,
                routingKey: MessagingConstants.RoutingKey,
                arguments: null,
                noWait: false,
                cancellationToken: cancellationToken);

            logger.LogInformation("RabbitMQ publisher connected to host {Host}.", _connectionFactory.HostName);
            return _channel;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null)
        {
            await _channel.DisposeAsync();
            _channel = null;
        }

        if (_connection is not null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }

        _connectionLock.Dispose();
    }
}
