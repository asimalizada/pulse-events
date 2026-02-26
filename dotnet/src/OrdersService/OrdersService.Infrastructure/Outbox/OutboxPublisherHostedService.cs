using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrdersService.Infrastructure.Messaging;
using OrdersService.Infrastructure.Persistence;

namespace OrdersService.Infrastructure.Outbox;

public sealed class OutboxPublisherHostedService(
    IServiceScopeFactory scopeFactory,
    IRabbitMqPublisher rabbitMqPublisher,
    ILogger<OutboxPublisherHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PublishPendingMessagesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Outbox publisher loop failed.");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task PublishPendingMessagesAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();

        var pendingMessages = await dbContext.OutboxMessages
            .Where(x => x.Status == "Pending")
            .OrderBy(x => x.CreatedAt)
            .Take(20)
            .ToListAsync(cancellationToken);

        if (pendingMessages.Count == 0)
        {
            return;
        }

        foreach (var message in pendingMessages)
        {
            try
            {
                await rabbitMqPublisher.PublishOrderCreatedAsync(message.Payload, cancellationToken);

                message.Status = "Published";
                message.PublishedAt = DateTimeOffset.UtcNow;

                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Failed to publish outbox message {OutboxMessageId} (EventId: {EventId}).",
                    message.Id,
                    message.EventId);
            }
        }
    }
}
