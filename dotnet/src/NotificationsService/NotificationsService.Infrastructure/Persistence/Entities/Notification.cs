namespace NotificationsService.Infrastructure.Persistence.Entities;

public sealed class Notification
{
    public Guid Id { get; set; }

    public Guid OrderId { get; set; }

    public string Message { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
}
