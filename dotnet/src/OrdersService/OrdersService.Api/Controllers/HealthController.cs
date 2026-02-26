using Microsoft.AspNetCore.Mvc;

namespace OrdersService.Api.Controllers;

[ApiController]
[Route("health")]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok();
}
