using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WhaleDeck.Api.Security;
using WhaleDeck.Application.Models;
using WhaleDeck.Application.Services;

namespace WhaleDeck.Api.Controllers;

[ApiController]
[Authorize(Policy = "Administrator")]
[Route("api/v1/identity/role-mappings")]
public sealed class RoleMappingsController(RoleMappingService mappings) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken) =>
        Ok(await mappings.ListAsync(cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveRoleMappingCommand command, CancellationToken cancellationToken) =>
        Ok(await mappings.SaveAsync(User.RequireSubject(), HttpContext.TraceIdentifier,
            command with { Id = null, ExpectedVersion = null }, cancellationToken));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] SaveRoleMappingCommand command, CancellationToken cancellationToken) =>
        Ok(await mappings.SaveAsync(User.RequireSubject(), HttpContext.TraceIdentifier,
            command with { Id = id }, cancellationToken));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] long version, CancellationToken cancellationToken)
    {
        await mappings.DeleteAsync(User.RequireSubject(), HttpContext.TraceIdentifier, id, version, cancellationToken);
        return NoContent();
    }
}
