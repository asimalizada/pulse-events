namespace NotificationsService.Api.Contracts;

public sealed record NotificationResponse(Guid Id, Guid OrderId, string Message, DateTimeOffset CreatedAt);
