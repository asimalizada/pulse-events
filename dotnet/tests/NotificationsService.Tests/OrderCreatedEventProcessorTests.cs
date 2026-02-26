using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NotificationsService.Domain;
using NotificationsService.Domain.Contracts;
using NotificationsService.Infrastructure.Consumers;
using NotificationsService.Infrastructure.Persistence;
using Xunit;

namespace NotificationsService.Tests;

public sealed class OrderCreatedEventProcessorTests
{
    [Fact]
    public async Task ProcessAsync_DuplicateEvent_IsIgnored()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<NotificationsDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var dbContext = new NotificationsDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var processor = new OrderCreatedEventProcessor(dbContext, NullLogger<OrderCreatedEventProcessor>.Instance);
        var eventId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var message = new OrderCreatedIntegrationEvent(
            eventId,
            MessagingConstants.EventTypeOrderCreated,
            new OrderCreatedIntegrationData(orderId, "123", 100m),
            DateTimeOffset.UtcNow,
            Guid.NewGuid());

        var firstResult = await processor.ProcessAsync(message, CancellationToken.None);
        var secondResult = await processor.ProcessAsync(message, CancellationToken.None);

        Assert.Equal(OrderCreatedProcessingResult.Processed, firstResult);
        Assert.Equal(OrderCreatedProcessingResult.Duplicate, secondResult);
        Assert.Equal(1, await dbContext.ProcessedEvents.CountAsync());
        Assert.Equal(1, await dbContext.Notifications.CountAsync());
    }
}
