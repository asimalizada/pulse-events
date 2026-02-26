using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using OrdersService.Api.Contracts;
using OrdersService.Api.Services;
using OrdersService.Infrastructure.Outbox;
using OrdersService.Infrastructure.Persistence;
using Xunit;

namespace OrdersService.Tests;

public sealed class OrdersApplicationServiceTests
{
    [Fact]
    public async Task CreateAsync_PersistsOrderAndOutboxInSameUnitOfWork()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<OrdersDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var dbContext = new OrdersDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var service = new OrdersApplicationService(dbContext, NullLogger<OrdersApplicationService>.Instance);
        var correlationId = Guid.NewGuid();

        var result = await service.CreateAsync(
            new CreateOrderRequest
            {
                CustomerId = "123",
                Amount = 100m,
            },
            correlationId,
            CancellationToken.None);

        var order = await dbContext.Orders.SingleAsync();
        var outbox = await dbContext.OutboxMessages.SingleAsync();

        Assert.Equal(result.OrderId, order.Id);
        Assert.Equal(result.EventId, outbox.EventId);
        Assert.Equal(OutboxStatus.Pending, outbox.Status);

        using var payload = JsonDocument.Parse(outbox.Payload);
        Assert.Equal(result.EventId.ToString(), payload.RootElement.GetProperty("eventId").GetString());
        Assert.Equal(correlationId.ToString(), payload.RootElement.GetProperty("correlationId").GetString());
        Assert.Equal(order.Id.ToString(), payload.RootElement.GetProperty("data").GetProperty("orderId").GetString());
    }
}
