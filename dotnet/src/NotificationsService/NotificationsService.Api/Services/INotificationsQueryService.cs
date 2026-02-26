using NotificationsService.Api.Contracts;

namespace NotificationsService.Api.Services;

public interface INotificationsQueryService
{
    Task<IReadOnlyList<NotificationResponse>> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken);
}
