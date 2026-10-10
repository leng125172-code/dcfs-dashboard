using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WhaleDeck.Application.Abstractions;

namespace WhaleDeck.Api.Controllers;

[ApiController]
[Authorize(Policy = "Administrator")]
[Route("api/v1/platform/diagnostics")]
public sealed partial class PlatformDiagnosticsController(IAgentGateway agent) : ControllerBase
{
    [HttpGet("{bundleId}")]
    public async Task<IActionResult> Download(string bundleId, CancellationToken cancellationToken)
    {
        if (!BundleIdPattern().IsMatch(bundleId)) return BadRequest();
        var content = await agent.DownloadDiagnosticBundleAsync(bundleId, cancellationToken);
        return File(content, "application/gzip", $"whaledeck-diagnostics-{bundleId}.tar.gz");
    }

    [GeneratedRegex("^[a-f0-9]{32}$", RegexOptions.CultureInvariant)]
    private static partial Regex BundleIdPattern();
}
