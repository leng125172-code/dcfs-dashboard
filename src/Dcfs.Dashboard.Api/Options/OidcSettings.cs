namespace Dcfs.Dashboard.Api.Options;

public sealed class OidcSettings
{
    public const string SectionName = "Authentication:Oidc";

    public bool Enabled { get; init; }

    public string Authority { get; init; } = string.Empty;

    public string ClientId { get; init; } = string.Empty;

    public string ClientSecret { get; init; } = string.Empty;

    public string CallbackPath { get; init; } = "/signin-oidc";

    public string SignedOutCallbackPath { get; init; } = "/signout-callback-oidc";
}
