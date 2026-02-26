using Microsoft.EntityFrameworkCore;
using NotificationsService.Api.Contracts;
using NotificationsService.Infrastructure.Persistence;

namespace NotificationsService.Api.Services;

public sealed class NotificationsQueryService(NotificationsDbContext dbContext) : INotificationsQueryService
{
    public async Task<IReadOnlyList<NotificationResponse>> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken)
    {
        return await dbContext.Notifications
            .AsNoTracking()
            .Where(n => n.OrderId == orderId)
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => new NotificationResponse(n.Id, n.OrderId, n.Message, n.CreatedAt))
            .ToListAsync(cancellationToken);
    }
}
