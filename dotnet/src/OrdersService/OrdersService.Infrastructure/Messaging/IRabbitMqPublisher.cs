namespace OrdersService.Infrastructure.Messaging;

public interface IRabbitMqPublisher
{
    Task PublishOrderCreatedAsync(string payload, CancellationToken cancellationToken);
}
