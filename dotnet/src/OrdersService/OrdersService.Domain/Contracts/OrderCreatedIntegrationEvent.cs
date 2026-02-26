using System.Text.Json.Serialization;

namespace OrdersService.Domain.Contracts;

public sealed record OrderCreatedIntegrationEvent(
    [property: JsonPropertyName("eventId")] Guid EventId,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("data")] OrderCreatedIntegrationData Data,
    [property: JsonPropertyName("occurredAt")] DateTimeOffset OccurredAt,
    [property: JsonPropertyName("correlationId")] Guid CorrelationId);

public sealed record OrderCreatedIntegrationData(
    [property: JsonPropertyName("orderId")] Guid OrderId,
    [property: JsonPropertyName("customerId")] string CustomerId,
    [property: JsonPropertyName("amount")] decimal Amount);
