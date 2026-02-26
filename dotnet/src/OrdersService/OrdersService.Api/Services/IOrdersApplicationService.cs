using OrdersService.Api.Contracts;

namespace OrdersService.Api.Services;

public interface IOrdersApplicationService
{
    Task<CreateOrderResult> CreateAsync(CreateOrderRequest request, CancellationToken cancellationToken);
}

public sealed record CreateOrderResult(Guid OrderId, Guid EventId);
