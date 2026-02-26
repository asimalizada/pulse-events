using OrdersService.Infrastructure.Persistence.Entities;

namespace OrdersService.Infrastructure.Messaging;

public interface IRabbitMqPublisher
{
    Task PublishOrderCreatedAsync(OutboxMessage message, CancellationToken cancellationToken);
    Task<bool> CanConnectAsync(CancellationToken cancellationToken);
}
