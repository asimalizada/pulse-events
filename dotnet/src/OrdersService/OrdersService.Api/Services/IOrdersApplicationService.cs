using OrdersService.Api.Contracts;

namespace OrdersService.Api.Services;

public interface IOrdersApplicationService
{
    Task<CreateOrderResult> CreateAsync(CreateOrderRequest request, Guid correlationId, CancellationToken cancellationToken);
}

public sealed record CreateOrderResult(Guid OrderId, Guid EventId, Guid CorrelationId);
