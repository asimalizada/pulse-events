using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Data.Sqlite;
using NotificationsService.Domain.Contracts;
using NotificationsService.Infrastructure.Persistence;
using NotificationsService.Infrastructure.Persistence.Entities;
using Npgsql;
using Serilog.Context;

namespace NotificationsService.Infrastructure.Consumers;

public interface IOrderCreatedEventProcessor
{
    Task<OrderCreatedProcessingResult> ProcessAsync(OrderCreatedIntegrationEvent message, CancellationToken cancellationToken);
}

public enum OrderCreatedProcessingResult
{
    Processed,
    Duplicate,
}

public sealed class OrderCreatedEventProcessor(
    NotificationsDbContext dbContext,
    ILogger<OrderCreatedEventProcessor> logger) : IOrderCreatedEventProcessor
{
    public async Task<OrderCreatedProcessingResult> ProcessAsync(
        OrderCreatedIntegrationEvent message,
        CancellationToken cancellationToken)
    {
        using (LogContext.PushProperty("OrderId", message.Data.OrderId))
        using (LogContext.PushProperty("EventId", message.EventId))
        using (LogContext.PushProperty("CorrelationId", message.CorrelationId))
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            dbContext.ProcessedEvents.Add(new ProcessedEvent
            {
                Id = Guid.NewGuid(),
                EventId = message.EventId,
                ProcessedAt = DateTimeOffset.UtcNow,
            });

            dbContext.Notifications.Add(new Notification
            {
                Id = Guid.NewGuid(),
                OrderId = message.Data.OrderId,
                Message = $"OrderCreated for customer {message.Data.CustomerId} amount {message.Data.Amount}",
                CreatedAt = DateTimeOffset.UtcNow,
            });

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                logger.LogInformation("OrderCreated event processed.");
                return OrderCreatedProcessingResult.Processed;
            }
            catch (DbUpdateException dbEx) when (IsDuplicateProcessedEvent(dbEx))
            {
                await transaction.RollbackAsync(cancellationToken);
                logger.LogInformation("Duplicate OrderCreated event detected; skipping.");
                return OrderCreatedProcessingResult.Duplicate;
            }
        }
    }

    private static bool IsDuplicateProcessedEvent(DbUpdateException exception)
    {
        if (exception.InnerException is PostgresException postgresException &&
            postgresException.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return true;
        }

        return exception.InnerException is SqliteException sqliteException &&
               sqliteException.SqliteErrorCode == 19;
    }
}
