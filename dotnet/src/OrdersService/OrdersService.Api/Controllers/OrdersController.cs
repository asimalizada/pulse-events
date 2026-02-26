using Microsoft.AspNetCore.Mvc;
using OrdersService.Api.Contracts;
using OrdersService.Api.Services;
using OrdersService.Domain;

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

        var correlationId = ResolveCorrelationId();
        var result = await ordersApplicationService.CreateAsync(request, correlationId, cancellationToken);
        Response.Headers[MessagingConstants.CorrelationHeader] = result.CorrelationId.ToString();

        return Created($"/orders/{result.OrderId}", new CreateOrderResponse(result.OrderId, result.EventId, result.CorrelationId));
    }

    private Guid ResolveCorrelationId()
    {
        var rawCorrelationId = Request.Headers[MessagingConstants.CorrelationHeader].FirstOrDefault();
        return Guid.TryParse(rawCorrelationId, out var correlationId) ? correlationId : Guid.NewGuid();
    }
}
