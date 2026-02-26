using System.Text.Json;
using OrdersService.Api.Contracts;
using OrdersService.Infrastructure.Persistence;
using OrdersService.Infrastructure.Persistence.Entities;

namespace OrdersService.Api.Services;

public sealed class OrdersApplicationService(OrdersDbContext dbContext) : IOrdersApplicationService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public async Task<CreateOrderResult> CreateAsync(CreateOrderRequest request, CancellationToken cancellationToken)
    {
        var orderId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var occurredAt = DateTimeOffset.UtcNow;

        var order = new Order
        {
            Id = orderId,
            CustomerId = request.CustomerId,
            Amount = request.Amount,
            CreatedAt = occurredAt,
        };

        var integrationEvent = new OrderCreatedEvent(
            eventId,
            "OrderCreated",
            new OrderCreatedEventData(orderId, request.CustomerId, request.Amount),
            occurredAt);

        var outboxMessage = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            Type = "OrderCreated",
            Payload = JsonSerializer.Serialize(integrationEvent, SerializerOptions),
            Status = "Pending",
            CreatedAt = occurredAt,
        };

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        dbContext.Orders.Add(order);
        dbContext.OutboxMessages.Add(outboxMessage);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new CreateOrderResult(orderId, eventId);
    }
}
