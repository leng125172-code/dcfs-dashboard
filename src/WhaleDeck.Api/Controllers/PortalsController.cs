using Microsoft.AspNetCore.Mvc;
using WhaleDeck.Api.Security;
using WhaleDeck.Application.Abstractions;
using WhaleDeck.Application.Models;
using WhaleDeck.Application.Services;

namespace WhaleDeck.Api.Controllers;

[ApiController]
[Route("api/v1/portals")]
public sealed class PortalsController(PortalService portals, IIdentityDirectory identityDirectory) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var current = await Current(cancellationToken);
        return Ok(await portals.ListAsync(current.Subject, current.IsAdministrator, cancellationToken));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SavePortalItemCommand command, CancellationToken cancellationToken)
    {
        var current = await Current(cancellationToken);
        var saved = await portals.SaveAsync(current.Subject, current.IsAdministrator, command with { Id = null }, cancellationToken);
        return CreatedAtAction(nameof(List), new { id = saved.Id }, saved);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] SavePortalItemCommand command, CancellationToken cancellationToken)
    {
        var current = await Current(cancellationToken);
        return Ok(await portals.SaveAsync(current.Subject, current.IsAdministrator, command with { Id = id }, cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var current = await Current(cancellationToken);
        await portals.DeleteAsync(current.Subject, current.IsAdministrator, id, cancellationToken);
        return NoContent();
    }

    private Task<CurrentUserDto> Current(CancellationToken cancellationToken) => identityDirectory.ResolveCurrentAsync(
        User.RequireSubject(), User.Identity?.Name, User.FindAll("groups").Select(item => item.Value).ToArray(), cancellationToken);
}
