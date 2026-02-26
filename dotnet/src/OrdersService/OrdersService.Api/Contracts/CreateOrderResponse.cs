namespace OrdersService.Api.Contracts;

public sealed record CreateOrderResponse(Guid OrderId, Guid EventId, Guid CorrelationId);
