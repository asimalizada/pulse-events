using Microsoft.AspNetCore.Mvc;
using OrdersService.Api.Contracts;
using OrdersService.Api.Services;

namespace OrdersService.Api.Controllers;

[ApiController]
[Route("orders")]
public sealed class OrdersController(IOrdersApplicationService ordersApplicationService) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOrderRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var result = await ordersApplicationService.CreateAsync(request, cancellationToken);

        return Created($"/orders/{result.OrderId}", new CreateOrderResponse(result.OrderId, result.EventId));
    }
}
