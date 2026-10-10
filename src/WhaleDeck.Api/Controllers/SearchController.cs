using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WhaleDeck.Api.Security;
using WhaleDeck.Application.Abstractions;
using WhaleDeck.Application.Services;

namespace WhaleDeck.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/search")]
public sealed class SearchController(GlobalSearchService search, IIdentityDirectory identity) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string q, [FromQuery] int take = 30, CancellationToken cancellationToken = default)
    {
        var subject = User.RequireSubject();
        var current = await identity.ResolveCurrentAsync(subject, User.Identity?.Name,
            User.FindAll("groups").Select(item => item.Value).ToArray(), cancellationToken);
        return Ok(await search.SearchAsync(subject, current.IsAdministrator, q, take, cancellationToken));
    }
}
