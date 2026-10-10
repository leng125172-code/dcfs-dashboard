using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WhaleDeck.Application.Abstractions;

namespace WhaleDeck.Api.Controllers;

[ApiController]
[Authorize(Policy = "Administrator")]
[Route("api/v1/host")]
public sealed class HostController(IAgentGateway agent) : ControllerBase
{
    [HttpGet("network-interfaces")]
    public async Task<IActionResult> NetworkInterfaces(CancellationToken cancellationToken) =>
        Ok(await agent.ListResourcesAsync("host-network-interfaces", cancellationToken));

    [HttpGet("storage-devices")]
    public async Task<IActionResult> StorageDevices(CancellationToken cancellationToken) =>
        Ok(await agent.ListResourcesAsync("host-storage-devices", cancellationToken));

    [HttpGet("journal")]
    public async Task<IActionResult> Journal(
        [FromQuery] string? unit,
        [FromQuery] int take = 200,
        [FromQuery] int sinceMinutes = 1440,
        [FromQuery] string? priority = null,
        [FromQuery] string? keyword = null,
        CancellationToken cancellationToken = default) =>
        Ok(await agent.QueryJournalAsync(unit, take, sinceMinutes, priority, keyword, cancellationToken));
}
