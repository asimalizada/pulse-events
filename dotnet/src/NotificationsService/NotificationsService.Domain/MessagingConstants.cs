namespace NotificationsService.Domain;

public static class MessagingConstants
{
    public const string ExchangeName = "pulse.events";
    public const string QueueName = "order.created";
    public const string RoutingKey = "order.created";
    public const string EventTypeOrderCreated = "OrderCreated";
    public const string CorrelationHeader = "x-correlation-id";
    public const string RetryCountHeader = "x-retry-count";
}
