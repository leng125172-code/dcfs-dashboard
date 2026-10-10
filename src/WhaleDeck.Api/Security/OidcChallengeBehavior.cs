namespace WhaleDeck.Api.Security;

internal static class OidcChallengeBehavior
{
    public static bool ShouldReturnUnauthorized(HttpRequest request) =>
        request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase) &&
        !request.Path.StartsWithSegments("/api/v1/auth/login", StringComparison.OrdinalIgnoreCase);
}
