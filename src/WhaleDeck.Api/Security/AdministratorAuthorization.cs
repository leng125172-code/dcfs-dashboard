using Microsoft.AspNetCore.Authorization;
using WhaleDeck.Application.Abstractions;

namespace WhaleDeck.Api.Security;

public sealed class AdministratorRequirement : IAuthorizationRequirement;

public sealed class AdministratorAuthorizationHandler(IIdentityDirectory identityDirectory)
    : AuthorizationHandler<AdministratorRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, AdministratorRequirement requirement)
    {
        var subject = context.User.FindFirst("sub")?.Value;
        if (string.IsNullOrWhiteSpace(subject)) return;
        var groups = context.User.FindAll("groups").Select(item => item.Value).ToArray();
        var current = await identityDirectory.ResolveCurrentAsync(subject, context.User.Identity?.Name, groups, CancellationToken.None);
        if (current.IsAdministrator) context.Succeed(requirement);
    }
}

public static class ClaimsPrincipalExtensions
{
    public static string RequireSubject(this System.Security.Claims.ClaimsPrincipal principal) =>
        principal.FindFirst("sub")?.Value ?? throw new UnauthorizedAccessException("Authenticated subject claim is missing.");
}
