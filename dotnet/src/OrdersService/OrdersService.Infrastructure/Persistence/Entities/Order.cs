namespace OrdersService.Infrastructure.Persistence.Entities;

public sealed class Order
{
    public Guid Id { get; set; }

    public string CustomerId { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
