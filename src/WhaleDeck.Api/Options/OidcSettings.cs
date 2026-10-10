namespace WhaleDeck.Api.Options;

public sealed class OidcSettings
{
    public const string SectionName = "Authentication:Oidc";

    public bool Enabled { get; init; }

    public OidcEndpointSettings Internal { get; init; } = new();

    public OidcEndpointSettings External { get; init; } = new();
}

public sealed class OidcEndpointSettings
{
    public string Authority { get; init; } = string.Empty;
    public string BackchannelHost { get; init; } = "authentik";
    public int BackchannelPort { get; init; } = 9000;
    public string ClientId { get; init; } = string.Empty;
    public string ClientSecret { get; init; } = string.Empty;
    public string CallbackPath { get; init; } = "/signin-oidc";
    public string SignedOutCallbackPath { get; init; } = "/signout-callback-oidc";
}
