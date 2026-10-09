using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WhaleDeck.Api.Security;
using WhaleDeck.Application.Abstractions;

namespace WhaleDeck.Api.Controllers;

[ApiController]
[Authorize(Policy = "Administrator")]
[Route("api/v1/secrets")]
public sealed class SecretsController(IOneTimeSecretStore secrets) : ControllerBase
{
    [HttpPost("consume")]
    public async Task<IActionResult> Consume([FromBody] ConsumeSecretRequest request, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers.Pragma = "no-cache";
        var value = await secrets.ConsumeAsync(User.RequireSubject(), request.Token, cancellationToken);
        return value is null ? NotFound() : Ok(new { value });
    }

    public sealed record ConsumeSecretRequest(string Token);
}
