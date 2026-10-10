using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WhaleDeck.Application.Abstractions;
using WhaleDeck.Application.Models;
using WhaleDeck.Application.Services;

namespace WhaleDeck.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/services")]
public sealed class ServiceDirectoryController(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var whaleDeckUrl = $"{Request.Scheme}://{Request.Host}";
        var authentikUrl = $"{Request.Scheme}://{Request.Host.Host}:8081";
        var results = new List<ServiceEntryDto>
        {
            new("whaledeck", "Whale Deck", "工作站管理与个人服务门户", whaleDeckUrl, "Healthy", 0, now)
        };

        var apiBase = configuration["Authentication:Authentik:ApiBaseUrl"] ?? "http://authentik:9000/api/v3/";
        var status = "Unavailable";
        long? latency = null;
        if (Uri.TryCreate(apiBase, UriKind.Absolute, out var apiUri))
        {
            var healthUri = new Uri(apiUri.GetLeftPart(UriPartial.Authority) + "/-/health/live/");
            var stopwatch = Stopwatch.StartNew();
            try
            {
                using var response = await httpClientFactory.CreateClient("ServiceHealth").GetAsync(healthUri, cancellationToken);
                status = response.IsSuccessStatusCode ? "Healthy" : "Degraded";
                latency = stopwatch.ElapsedMilliseconds;
            }
            catch (HttpRequestException)
            {
                status = "Unavailable";
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                status = "Unavailable";
            }
        }
        results.Add(new ServiceEntryDto("authentik", "Authentik", "统一身份、用户与 SSO 服务",
            authentikUrl, status, latency, now));
        return Ok(results);
    }
}

[ApiController]
[Authorize(Policy = "Administrator")]
[Route("api/v1/agents")]
public sealed class AgentStatusController(IAgentGateway agent, OperationService operations) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var capabilities = await agent.GetCapabilitiesAsync(cancellationToken);
        var health = await agent.GetHealthAsync(cancellationToken);
        var jobs = (await operations.ListAsync(100, cancellationToken))
            .Where(item => item.State is "Queued" or "Running")
            .ToArray();
        return Ok(new AgentStatusDto(capabilities, health, jobs));
    }
}
