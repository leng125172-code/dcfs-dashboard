using Dcfs.Dashboard.Api.Options;
using Dcfs.Dashboard.Infrastructure;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Serilog;
using Serilog.Formatting.Json;

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console(new JsonFormatter())
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console(new JsonFormatter()));

    builder.Services
        .AddOptions<OidcSettings>()
        .Bind(builder.Configuration.GetSection(OidcSettings.SectionName))
        .Validate(settings => !settings.Enabled ||
                              (Uri.TryCreate(settings.Authority, UriKind.Absolute, out _) &&
                               !string.IsNullOrWhiteSpace(settings.ClientId) &&
                               !string.IsNullOrWhiteSpace(settings.ClientSecret)),
            "Enabled OIDC requires a valid Authority, ClientId and ClientSecret.")
        .ValidateOnStart();

    var oidcSettings = builder.Configuration
        .GetSection(OidcSettings.SectionName)
        .Get<OidcSettings>() ?? new OidcSettings();

    if (!oidcSettings.Enabled && !builder.Environment.IsDevelopment())
    {
        throw new InvalidOperationException("OIDC authentication must be enabled outside Development.");
    }

    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddControllers();
    builder.Services.AddProblemDetails();
    builder.Services.AddOpenApi();
    builder.Services.AddHealthChecks();

    if (oidcSettings.Enabled)
    {
        builder.Services
            .AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
            })
            .AddCookie(options =>
            {
                options.Cookie.Name = "dcfs.dashboard.session";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = CookieSecurePolicy.None;
                options.SlidingExpiration = true;
            })
            .AddOpenIdConnect(options =>
            {
                options.Authority = oidcSettings.Authority;
                options.ClientId = oidcSettings.ClientId;
                options.ClientSecret = oidcSettings.ClientSecret;
                options.ResponseType = OpenIdConnectResponseType.Code;
                options.UsePkce = true;
                options.SaveTokens = false;
                options.GetClaimsFromUserInfoEndpoint = true;
                options.MapInboundClaims = false;
                options.RequireHttpsMetadata = false;
                options.CallbackPath = oidcSettings.CallbackPath;
                options.SignedOutCallbackPath = oidcSettings.SignedOutCallbackPath;
                options.TokenValidationParameters.NameClaimType = "name";
                options.TokenValidationParameters.RoleClaimType = "groups";
            });

        builder.Services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build());
    }
    else
    {
        builder.Services.AddAuthorization();
    }

    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor |
                                   ForwardedHeaders.XForwardedHost |
                                   ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = 1;
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
    });

    var app = builder.Build();

    app.UseForwardedHeaders();
    app.UseExceptionHandler();
    app.UseSerilogRequestLogging(options =>
    {
        options.GetLevel = (httpContext, _, exception) =>
            exception is not null || httpContext.Response.StatusCode >= 500
                ? Serilog.Events.LogEventLevel.Error
                : Serilog.Events.LogEventLevel.Debug;
    });

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi().AllowAnonymous();
    }

    if (oidcSettings.Enabled)
    {
        app.UseAuthentication();
    }

    app.UseAuthorization();

    app.MapHealthChecks("/health/live").AllowAnonymous();
    app.MapControllers();

    app.Run();
}
catch (Exception exception)
{
    Log.Fatal(exception, "Dashboard API terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}

public partial class Program;
