namespace OrdersService.Infrastructure.Persistence.Entities;

public sealed class OutboxMessage
{
    public Guid Id { get; set; }

    public Guid EventId { get; set; }

    public string Type { get; set; } = string.Empty;

    public string Payload { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }
}
