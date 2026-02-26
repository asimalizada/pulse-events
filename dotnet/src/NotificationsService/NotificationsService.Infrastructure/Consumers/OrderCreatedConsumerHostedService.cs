using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NotificationsService.Domain;
using NotificationsService.Domain.Contracts;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Serilog.Context;

namespace NotificationsService.Infrastructure.Consumers;

public sealed class OrderCreatedConsumerHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<OrderCreatedConsumerHostedService> logger) : BackgroundService, IAsyncDisposable
{
    private const int MaxRetries = 5;

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
        await ConnectWithRetryAsync(stoppingToken);

        if (_channel is null)
        {
            return;
        }

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (_, eventArgs) => await OnMessageAsync(eventArgs, stoppingToken);

        await _channel.BasicConsumeAsync(
            queue: MessagingConstants.QueueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        logger.LogInformation("OrderCreated consumer started on queue {QueueName}.", MessagingConstants.QueueName);

        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // normal shutdown
        }
    }

    private async Task ConnectWithRetryAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await EnsureConnectedAsync(cancellationToken);
                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "RabbitMQ connection failed during consumer startup. Retrying in 2 seconds.");
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
        }
    }

    private async Task OnMessageAsync(BasicDeliverEventArgs eventArgs, CancellationToken cancellationToken)
    {
        if (_channel is null)
        {
            return;
        }

        OrderCreatedIntegrationEvent? message = null;
        try
        {
            message = JsonSerializer.Deserialize<OrderCreatedIntegrationEvent>(eventArgs.Body.Span, SerializerOptions)
                ?? throw new InvalidOperationException("Message body is empty.");

            if (!string.Equals(message.Type, MessagingConstants.EventTypeOrderCreated, StringComparison.Ordinal))
            {
                logger.LogWarning("Unexpected event type {EventType}. ACKing message.", message.Type);
                await _channel.BasicAckAsync(eventArgs.DeliveryTag, multiple: false, cancellationToken: cancellationToken);
                return;
            }

            if (message.EventId == Guid.Empty || message.Data.OrderId == Guid.Empty || string.IsNullOrWhiteSpace(message.Data.CustomerId))
            {
                throw new InvalidOperationException("Event payload is invalid.");
            }

            using (LogContext.PushProperty("OrderId", message.Data.OrderId))
            using (LogContext.PushProperty("EventId", message.EventId))
            using (LogContext.PushProperty("CorrelationId", message.CorrelationId))
            {
                using var scope = scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<IOrderCreatedEventProcessor>();
                await processor.ProcessAsync(message, cancellationToken);

                await _channel.BasicAckAsync(eventArgs.DeliveryTag, multiple: false, cancellationToken: cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process message.");
            await HandleFailureWithRetryAsync(eventArgs, message, cancellationToken);
        }
    }

    private async Task HandleFailureWithRetryAsync(
        BasicDeliverEventArgs eventArgs,
        OrderCreatedIntegrationEvent? message,
        CancellationToken cancellationToken)
    {
        if (_channel is null)
        {
            return;
        }

        var retryCount = GetRetryCount(eventArgs.BasicProperties?.Headers);
        if (retryCount >= MaxRetries)
        {
            logger.LogError(
                "Message exceeded max retries ({MaxRetries}). Rejecting without requeue. EventId: {EventId}",
                MaxRetries,
                message?.EventId);

            await _channel.BasicRejectAsync(eventArgs.DeliveryTag, requeue: false, cancellationToken);
            return;
        }

        var properties = new BasicProperties
        {
            Persistent = true,
            ContentType = eventArgs.BasicProperties?.ContentType ?? "application/json",
            Type = eventArgs.BasicProperties?.Type ?? message?.Type ?? MessagingConstants.EventTypeOrderCreated,
            MessageId = eventArgs.BasicProperties?.MessageId ?? message?.EventId.ToString(),
            Headers = CloneHeaders(eventArgs.BasicProperties?.Headers),
        };

        properties.Headers[MessagingConstants.RetryCountHeader] = retryCount + 1;
        if (message is not null)
        {
            properties.Headers[MessagingConstants.CorrelationHeader] = message.CorrelationId.ToString();
        }

        await _channel.BasicPublishAsync(
            exchange: MessagingConstants.ExchangeName,
            routingKey: MessagingConstants.RoutingKey,
            mandatory: true,
            basicProperties: properties,
            body: eventArgs.Body,
            cancellationToken: cancellationToken);

        await _channel.BasicAckAsync(eventArgs.DeliveryTag, multiple: false, cancellationToken: cancellationToken);
    }

    private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
    {
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

        await _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 20, global: false, cancellationToken: cancellationToken);

        logger.LogInformation("RabbitMQ consumer connected to host {Host}.", _connectionFactory.HostName);
    }

    private static int GetRetryCount(IDictionary<string, object?>? headers)
    {
        if (headers is null || !headers.TryGetValue(MessagingConstants.RetryCountHeader, out var value) || value is null)
        {
            return 0;
        }

        if (value is byte[] bytes && int.TryParse(Encoding.UTF8.GetString(bytes), out var fromBytes))
        {
            return fromBytes;
        }

        if (value is int intValue)
        {
            return intValue;
        }

        if (value is long longValue && longValue <= int.MaxValue)
        {
            return (int)longValue;
        }

        return 0;
    }

    private static IDictionary<string, object?> CloneHeaders(IDictionary<string, object?>? headers)
    {
        var clone = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (headers is null)
        {
            return clone;
        }

        foreach (var pair in headers)
        {
            clone[pair.Key] = pair.Value;
        }

        return clone;
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
}
