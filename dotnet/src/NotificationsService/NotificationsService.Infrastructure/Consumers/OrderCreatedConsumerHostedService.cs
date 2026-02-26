using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NotificationsService.Infrastructure.Persistence;
using NotificationsService.Infrastructure.Persistence.Entities;
using Npgsql;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace NotificationsService.Infrastructure.Consumers;

public sealed class OrderCreatedConsumerHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<OrderCreatedConsumerHostedService> logger) : BackgroundService, IAsyncDisposable
{
    private const string ExchangeName = "pulse.events";
    private const string QueueName = "order.created";
    private const string RoutingKey = "order.created";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

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

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await EnsureConnectedAsync(stoppingToken);

        if (_channel is null)
        {
            throw new InvalidOperationException("RabbitMQ channel was not initialized.");
        }

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (_, eventArgs) => await OnMessageAsync(eventArgs, stoppingToken);

        await _channel.BasicConsumeAsync(
            queue: QueueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        logger.LogInformation("OrderCreated consumer started on queue {QueueName}.", QueueName);

        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // normal shutdown
        }
    }

    private async Task OnMessageAsync(BasicDeliverEventArgs eventArgs, CancellationToken cancellationToken)
    {
        if (_channel is null)
        {
            return;
        }

        try
        {
            var message = JsonSerializer.Deserialize<OrderCreatedEventEnvelope>(eventArgs.Body.Span, SerializerOptions)
                ?? throw new InvalidOperationException("Message body is empty.");

            if (message.EventId == Guid.Empty)
            {
                throw new InvalidOperationException("eventId is missing.");
            }

            if (!string.Equals(message.Type, "OrderCreated", StringComparison.Ordinal))
            {
                logger.LogWarning("Unexpected event type {EventType}. ACKing message.", message.Type);
                await _channel.BasicAckAsync(eventArgs.DeliveryTag, multiple: false, cancellationToken: cancellationToken);
                return;
            }

            if (message.Data is null || message.Data.OrderId == Guid.Empty || string.IsNullOrWhiteSpace(message.Data.CustomerId))
            {
                throw new InvalidOperationException("Event data is invalid.");
            }

            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();

            var alreadyProcessed = await dbContext.ProcessedEvents
                .AsNoTracking()
                .AnyAsync(x => x.EventId == message.EventId, cancellationToken);

            if (alreadyProcessed)
            {
                await _channel.BasicAckAsync(eventArgs.DeliveryTag, multiple: false, cancellationToken: cancellationToken);
                return;
            }

            dbContext.ProcessedEvents.Add(new ProcessedEvent
            {
                Id = Guid.NewGuid(),
                EventId = message.EventId,
                ProcessedAt = DateTimeOffset.UtcNow,
            });

            dbContext.Notifications.Add(new Notification
            {
                Id = Guid.NewGuid(),
                OrderId = message.Data.OrderId,
                Message = $"OrderCreated for customer {message.Data.CustomerId} amount {message.Data.Amount}",
                CreatedAt = DateTimeOffset.UtcNow,
            });

            await dbContext.SaveChangesAsync(cancellationToken);

            await _channel.BasicAckAsync(eventArgs.DeliveryTag, multiple: false, cancellationToken: cancellationToken);
        }
        catch (DbUpdateException dbEx) when (IsDuplicateProcessedEvent(dbEx))
        {
            logger.LogInformation(dbEx, "Duplicate event detected during insert; ACKing message.");
            await _channel.BasicAckAsync(eventArgs.DeliveryTag, multiple: false, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process message; NACK with requeue=true.");
            await _channel.BasicNackAsync(eventArgs.DeliveryTag, multiple: false, requeue: true, cancellationToken: cancellationToken);
        }
    }

    private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        _connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);

        await _channel.ExchangeDeclareAsync(
            exchange: ExchangeName,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            arguments: null,
            passive: false,
            noWait: false,
            cancellationToken: cancellationToken);

        await _channel.QueueDeclareAsync(
            queue: QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            passive: false,
            noWait: false,
            cancellationToken: cancellationToken);

        await _channel.QueueBindAsync(
            queue: QueueName,
            exchange: ExchangeName,
            routingKey: RoutingKey,
            arguments: null,
            noWait: false,
            cancellationToken: cancellationToken);

        await _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 20, global: false, cancellationToken: cancellationToken);

        logger.LogInformation("RabbitMQ consumer connected to host {Host}.", _connectionFactory.HostName);
    }

    private static bool IsDuplicateProcessedEvent(DbUpdateException exception)
    {
        return exception.InnerException is PostgresException postgresException &&
               postgresException.SqlState == PostgresErrorCodes.UniqueViolation;
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
    }

    private sealed record OrderCreatedEventEnvelope(
        [property: JsonPropertyName("eventId")] Guid EventId,
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("data")] OrderCreatedEventData Data);

    private sealed record OrderCreatedEventData(
        [property: JsonPropertyName("orderId")] Guid OrderId,
        [property: JsonPropertyName("customerId")] string CustomerId,
        [property: JsonPropertyName("amount")] decimal Amount);
}
