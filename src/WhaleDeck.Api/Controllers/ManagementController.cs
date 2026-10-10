using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using WhaleDeck.Api.Security;
using WhaleDeck.Application.Abstractions;
using WhaleDeck.Application.Models;
using WhaleDeck.Application.Services;

namespace WhaleDeck.Api.Controllers;

[ApiController]
[Authorize(Policy = "Administrator")]
[Route("api/v1")]
public sealed class ManagementController(
    IAgentGateway agent,
    IManagementQuery queries,
    ICatalogProvider catalog,
    IIdentityDirectory identity,
    OperationService operations) : ControllerBase
{
    [HttpGet("containers")]
    public async Task<IActionResult> Containers([FromQuery] bool includeStopped = true, CancellationToken cancellationToken = default) =>
        Ok(await agent.ListContainersAsync(includeStopped, cancellationToken));

    [HttpGet("applications/popular")]
    public async Task<IActionResult> Popular(CancellationToken cancellationToken) => Ok(await catalog.GetPopularAsync(cancellationToken));

    [HttpGet("users")]
    public async Task<IActionResult> Users(CancellationToken cancellationToken) => Ok(await identity.ListUsersAsync(cancellationToken));

    [HttpGet("groups")]
    public async Task<IActionResult> Groups(CancellationToken cancellationToken) => Ok(await identity.ListGroupsAsync(cancellationToken));

    [HttpGet("sso")]
    public async Task<IActionResult> Sso(CancellationToken cancellationToken) => Ok(await identity.ListSsoApplicationsAsync(cancellationToken));

    [HttpGet("config-repository/status")]
    public async Task<IActionResult> ConfigRepositoryStatus(CancellationToken cancellationToken) =>
        Ok(await agent.GetConfigRepositoryStatusAsync(cancellationToken));

    [HttpGet("docker/settings")]
    public async Task<IActionResult> DockerSettings(CancellationToken cancellationToken) =>
        Ok(await agent.GetDockerSettingsAsync(cancellationToken));

    [HttpPost("applications/image-metadata")]
    public async Task<IActionResult> ApplicationImageMetadata([FromBody] ImageMetadataRequest request, CancellationToken cancellationToken) =>
        Ok(await agent.InspectApplicationImageAsync(request.Image, request.PullIfMissing, cancellationToken));

    [HttpGet("{area:regex(^(images|networks|volumes|compose-projects|databases|applications|systemd)$)}")]
    public async Task<IActionResult> Resources(string area, CancellationToken cancellationToken) => Ok(await queries.ListAsync(area, cancellationToken));

    [HttpPost("{area:regex(^(containers|docker|databases|backups|applications|host|config-repository|platform)$)}/{operation}/plan")]
    public async Task<IActionResult> Plan(string area, string operation, [FromBody] PlanRequest request, CancellationToken cancellationToken) =>
        Ok(await agent.PlanAsync(area, operation, request.ResourceId, request.Parameters ?? new Dictionary<string, string>(), cancellationToken));

    [HttpPost("{area:regex(^(containers|docker|databases|backups|applications|host|config-repository|platform|identity)$)}/{operation}")]
    public async Task<IActionResult> Enqueue(string area, string operation, [FromBody] OperationRequest request, CancellationToken cancellationToken)
    {
        var idempotencyKey = Request.Headers["Idempotency-Key"].FirstOrDefault() ?? request.IdempotencyKey;
        var requestJson = JsonSerializer.Serialize(new { resourceId = request.ResourceId, parameters = request.Parameters ?? new Dictionary<string, string>(), planHash = request.PlanHash });
        var command = new OperationCommand(area, operation, request.ResourceId, idempotencyKey, requestJson, request.PlanHash, request.Confirmed);
        var job = await operations.EnqueueAsync(User.RequireSubject(), HttpContext.TraceIdentifier, command, cancellationToken);
        return AcceptedAtAction(nameof(JobsController.Get), "Jobs", new { id = job.Id }, job);
    }

    public sealed record OperationRequest(string? ResourceId, string IdempotencyKey, IReadOnlyDictionary<string, string>? Parameters, string? PlanHash, bool Confirmed);
    public sealed record PlanRequest(string? ResourceId, IReadOnlyDictionary<string, string>? Parameters);
    public sealed record ImageMetadataRequest(string Image, bool PullIfMissing = false);
}
