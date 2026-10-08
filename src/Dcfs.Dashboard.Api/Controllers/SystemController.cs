using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dcfs.Dashboard.Api.Controllers;

[ApiController]
[Route("api/v1/system")]
public sealed class SystemController : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("status")]
    public IActionResult Status()
    {
        return Ok(new
        {
            service = "dcfs-dashboard-api",
            status = "ok",
            utcTime = DateTimeOffset.UtcNow,
        });
    }
}
