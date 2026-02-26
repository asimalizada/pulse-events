namespace NotificationsService.Infrastructure.Persistence.Entities;

public sealed class ProcessedEvent
{
    public Guid Id { get; set; }

    public Guid EventId { get; set; }

    public DateTimeOffset ProcessedAt { get; set; }
}
