using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WhaleDeck.Application.Abstractions;

namespace WhaleDeck.Api.Controllers;

[ApiController]
[Authorize(Policy = "Administrator")]
[Route("api/v1/metrics")]
public sealed class MetricsController(IMetricsQuery metrics) : ControllerBase
{
    private static readonly HashSet<string> AllowedKinds = new(StringComparer.Ordinal)
    {
        "cpu.utilization", "memory.utilization", "disk.utilization", "disk.read", "disk.write",
        "network.receive", "network.send", "network.receive.total", "network.send.total"
    };

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string[] kinds, [FromQuery] int take = 60, CancellationToken cancellationToken = default)
    {
        if (kinds.Length == 0 || kinds.Any(kind => !AllowedKinds.Contains(kind)))
            throw new ArgumentException("One or more metric kinds are not approved.");
        return Ok(await metrics.GetHistoryAsync(kinds, take, cancellationToken));
    }
}
