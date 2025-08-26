using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuthService.Controllers;

[ApiController]
[Route("ping")]
[ApiExplorerSettings(IgnoreApi = true)]
public class PingController : ControllerBase
{
    [HttpGet]
    [AllowAnonymous] // remove if you want auth
    public IActionResult Get() => Ok("pong");
}
