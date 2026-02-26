using System.Text.Json;
using Microsoft.Extensions.Logging;
using OrdersService.Api.Contracts;
using OrdersService.Domain;
using OrdersService.Domain.Contracts;
using OrdersService.Infrastructure.Outbox;
using OrdersService.Infrastructure.Persistence;
using OrdersService.Infrastructure.Persistence.Entities;
using Serilog.Context;

namespace OrdersService.Api.Services;

public sealed class OrdersApplicationService(
    OrdersDbContext dbContext,
    ILogger<OrdersApplicationService> logger) : IOrdersApplicationService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public async Task<CreateOrderResult> CreateAsync(
        CreateOrderRequest request,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        var orderId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var occurredAt = DateTimeOffset.UtcNow;

        using (LogContext.PushProperty("OrderId", orderId))
        using (LogContext.PushProperty("EventId", eventId))
        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            var order = new Order
            {
                Id = orderId,
                CustomerId = request.CustomerId,
                Amount = request.Amount,
                CreatedAt = occurredAt,
            };

            var integrationEvent = new OrderCreatedIntegrationEvent(
                eventId,
                MessagingConstants.EventTypeOrderCreated,
                new OrderCreatedIntegrationData(orderId, request.CustomerId, request.Amount),
                occurredAt,
                correlationId);

            var outboxMessage = new OutboxMessage
            {
                Id = Guid.NewGuid(),
                EventId = eventId,
                Type = MessagingConstants.EventTypeOrderCreated,
                Payload = JsonSerializer.Serialize(integrationEvent, SerializerOptions),
                Status = OutboxStatus.Pending,
                CreatedAt = occurredAt,
            };

            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            dbContext.Orders.Add(order);
            dbContext.OutboxMessages.Add(outboxMessage);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            logger.LogInformation(
                "Order and outbox message persisted in a single transaction.");

            return new CreateOrderResult(orderId, eventId, correlationId);
        }
    }
}
