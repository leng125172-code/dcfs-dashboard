using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WhaleDeck.Api.Security;
using WhaleDeck.Application.Services;

namespace WhaleDeck.Api.Controllers;

[ApiController]
[Authorize(Policy = "Administrator")]
[Route("api/v1/jobs")]
public sealed class JobsController(OperationService operations) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int take = 50, CancellationToken cancellationToken = default) =>
        Ok(await operations.ListAsync(take, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var job = await operations.FindAsync(id, cancellationToken);
        return job is null ? NotFound() : Ok(job);
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        await operations.CancelAsync(id, User.RequireSubject(), cancellationToken);
        return Accepted();
    }

    [HttpGet("{id:guid}/events")]
    public async Task Events(Guid id, CancellationToken cancellationToken)
    {
        Response.Headers.ContentType = "text/event-stream";
        var after = long.TryParse(Request.Headers["Last-Event-ID"], out var sequence) ? sequence : 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            var events = await operations.EventsAsync(id, after, cancellationToken);
            foreach (var item in events)
            {
                await Response.WriteAsync($"id: {item.Sequence}\nevent: progress\ndata: {JsonSerializer.Serialize(item)}\n\n", cancellationToken);
                after = item.Sequence;
            }
            await Response.Body.FlushAsync(cancellationToken);
            var job = await operations.FindAsync(id, cancellationToken);
            if (job is null || job.State is "Succeeded" or "Failed" or "Canceled" or "RolledBack") return;
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
        }
    }
}
