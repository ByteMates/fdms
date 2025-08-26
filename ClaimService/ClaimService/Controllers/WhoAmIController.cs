using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("whoami")]
public class WhoAmIController : ControllerBase
{
    [HttpGet]
    [Authorize]
    public IActionResult Get()
    {
        var roles = User.Claims
            .Where(c => c.Type is "role" or "roles" || c.Type.EndsWith("/claims/role"))
            .Select(c => c.Value)
            .ToArray();

        return Ok(new
        {
            sub = User.FindFirst("sub")?.Value,
            name = User.Identity?.Name,
            roles
        });
    }
}
