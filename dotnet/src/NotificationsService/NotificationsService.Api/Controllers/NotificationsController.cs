using Microsoft.AspNetCore.Mvc;
using NotificationsService.Api.Services;

namespace NotificationsService.Api.Controllers;

[ApiController]
[Route("notifications")]
public sealed class NotificationsController(INotificationsQueryService notificationsQueryService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] Guid? orderId, CancellationToken cancellationToken)
    {
        if (orderId is null || orderId == Guid.Empty)
        {
            return BadRequest(new { message = "orderId query parameter is required." });
        }

        var notifications = await notificationsQueryService.GetByOrderIdAsync(orderId.Value, cancellationToken);

        return Ok(notifications);
    }
}
