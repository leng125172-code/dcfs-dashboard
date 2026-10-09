using Microsoft.AspNetCore.Mvc;
using WhaleDeck.Api.Security;
using WhaleDeck.Application.Abstractions;
using WhaleDeck.Application.Services;

namespace WhaleDeck.Api.Controllers;

[ApiController]
[Route("api/v1/overview")]
public sealed class OverviewController(OverviewService overview, IIdentityDirectory identityDirectory) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var subject = User.RequireSubject();
        var current = await identityDirectory.ResolveCurrentAsync(subject, User.Identity?.Name,
            User.FindAll("groups").Select(item => item.Value).ToArray(), cancellationToken);
        return Ok(await overview.GetAsync(subject, current.IsAdministrator, cancellationToken));
    }
}
