using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrdersService.Infrastructure.Messaging;
using OrdersService.Infrastructure.Persistence;
using Serilog.Context;

namespace OrdersService.Infrastructure.Outbox;

public sealed class OutboxPublisherHostedService(
    IServiceScopeFactory scopeFactory,
    IRabbitMqPublisher rabbitMqPublisher,
    ILogger<OutboxPublisherHostedService> logger) : BackgroundService
{
    private const int BatchSize = 20;
    private const int PublishAttempts = 3;
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
            .AsNoTracking()
            .Where(x => x.Status == OutboxStatus.Pending)
            .OrderBy(x => x.CreatedAt)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        foreach (var message in pendingMessages)
        {
            using (LogContext.PushProperty("EventId", message.EventId))
            {
                var published = await PublishWithRetryAsync(message, cancellationToken);
                if (!published)
                {
                    logger.LogError(
                        "Outbox message {OutboxMessageId} remains pending after retries.",
                        message.Id);
                    continue;
                }

                var updatedRows = await dbContext.OutboxMessages
                    .Where(x => x.Id == message.Id && x.Status == OutboxStatus.Pending)
                    .ExecuteUpdateAsync(
                        setters => setters
                            .SetProperty(x => x.Status, OutboxStatus.Published)
                            .SetProperty(x => x.PublishedAt, DateTimeOffset.UtcNow),
                        cancellationToken);

                if (updatedRows != 1)
                {
                    logger.LogWarning(
                        "Outbox status update skipped for message {OutboxMessageId}; it may have been updated concurrently.",
                        message.Id);
                }
            }
        }
    }

    private async Task<bool> PublishWithRetryAsync(
        Persistence.Entities.OutboxMessage message,
        CancellationToken cancellationToken)
    {
        var delay = TimeSpan.FromMilliseconds(200);
        for (var attempt = 1; attempt <= PublishAttempts; attempt++)
        {
            try
            {
                await rabbitMqPublisher.PublishOrderCreatedAsync(message, cancellationToken);
                logger.LogInformation(
                    "Outbox message {OutboxMessageId} published on attempt {Attempt}.",
                    message.Id,
                    attempt);
                return true;
            }
            catch (Exception ex) when (attempt < PublishAttempts)
            {
                logger.LogWarning(
                    ex,
                    "Publishing failed for outbox message {OutboxMessageId} on attempt {Attempt}; retrying.",
                    message.Id,
                    attempt);

                await Task.Delay(delay, cancellationToken);
                delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * 2);
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Publishing failed permanently for outbox message {OutboxMessageId}.",
                    message.Id);
                return false;
            }
        }

        return false;
    }
}
