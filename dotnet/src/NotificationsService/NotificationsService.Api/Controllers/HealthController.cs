using Microsoft.AspNetCore.Mvc;

namespace NotificationsService.Api.Controllers;

[ApiController]
[Route("health")]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok();
}
