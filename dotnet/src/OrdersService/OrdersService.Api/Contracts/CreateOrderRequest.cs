using System.ComponentModel.DataAnnotations;

namespace OrdersService.Api.Contracts;

public sealed class CreateOrderRequest
{
    [Required]
    public string CustomerId { get; init; } = string.Empty;

    [Range(typeof(decimal), "0.01", "79228162514264337593543950335")]
    public decimal Amount { get; init; }
}
