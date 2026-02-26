using System.Text.Json.Serialization;

namespace OrdersService.Api.Contracts;

public sealed record OrderCreatedEvent(
    [property: JsonPropertyName("eventId")] Guid EventId,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("data")] OrderCreatedEventData Data,
    [property: JsonPropertyName("occurredAt")] DateTimeOffset OccurredAt);

public sealed record OrderCreatedEventData(
    [property: JsonPropertyName("orderId")] Guid OrderId,
    [property: JsonPropertyName("customerId")] string CustomerId,
    [property: JsonPropertyName("amount")] decimal Amount);
