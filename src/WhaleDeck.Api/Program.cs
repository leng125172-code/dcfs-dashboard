using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Serilog;
using Serilog.Formatting.Json;
using WhaleDeck.Api.Middleware;
using WhaleDeck.Api.Options;
using WhaleDeck.Api.Security;
using WhaleDeck.Infrastructure;

Log.Logger = new LoggerConfiguration().Enrich.FromLogContext().WriteTo.Console(new JsonFormatter()).CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console(new JsonFormatter()));

    builder.Services.AddOptions<OidcSettings>()
        .Bind(builder.Configuration.GetSection(OidcSettings.SectionName))
        .Validate(settings => !settings.Enabled ||
            (Uri.TryCreate(settings.Authority, UriKind.Absolute, out _) &&
             !string.IsNullOrWhiteSpace(settings.ClientId) &&
             !string.IsNullOrWhiteSpace(settings.ClientSecret)),
            "Enabled OIDC requires a valid Authority, ClientId and ClientSecret.")
        .ValidateOnStart();

    var oidcSettings = builder.Configuration.GetSection(OidcSettings.SectionName).Get<OidcSettings>() ?? new();
    if (!oidcSettings.Enabled && !builder.Environment.IsDevelopment())
    {
        throw new InvalidOperationException("OIDC authentication must be enabled outside Development.");
    }

    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddControllers(options => options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
    builder.Services.AddProblemDetails();
    builder.Services.AddOpenApi();
    builder.Services.AddHealthChecks();
    builder.Services.AddAntiforgery(options =>
    {
        options.Cookie.Name = "whaledeck.xsrf";
        options.Cookie.HttpOnly = false;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = CookieSecurePolicy.None;
        options.HeaderName = "X-XSRF-TOKEN";
    });
    builder.Services.AddRateLimiter(options => options.AddPolicy("api", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.User.FindFirst("sub")?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 120, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })));

    if (oidcSettings.Enabled)
    {
        builder.Services.AddAuthentication(options =>
        {
            options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
        })
        .AddCookie(options =>
        {
            options.Cookie.Name = "whaledeck.session";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = CookieSecurePolicy.None;
            options.SlidingExpiration = true;
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
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
    }
    else
    {
        builder.Services.AddAuthentication(options => options.DefaultScheme = "Development")
            .AddScheme<AuthenticationSchemeOptions, DevelopmentAuthenticationHandler>("Development", _ => { });
    }

    builder.Services.AddScoped<IAuthorizationHandler, AdministratorAuthorizationHandler>();
    var authorization = builder.Services.AddAuthorizationBuilder()
        .AddPolicy("Administrator", policy => policy.RequireAuthenticatedUser().AddRequirements(new AdministratorRequirement()));
    authorization.SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedHost | ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = 1;
    });

    var app = builder.Build();
    app.UseForwardedHeaders();
    app.UseExceptionHandler(error => error.Run(TraceAndErrors.WriteProblemAsync));
    app.UseSerilogRequestLogging(options => options.GetLevel = (context, _, exception) =>
        exception is not null || context.Response.StatusCode >= 500
            ? Serilog.Events.LogEventLevel.Error
            : Serilog.Events.LogEventLevel.Debug);
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseAntiforgery();

    if (!app.Environment.IsDevelopment())
    {
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/swagger"))
            {
                var service = context.RequestServices.GetRequiredService<IAuthorizationService>();
                if (!(await service.AuthorizeAsync(context.User, "Administrator")).Succeeded)
                {
                    await context.ChallengeAsync();
                    return;
                }
            }
            await next();
        });
    }

    app.UseSwaggerUI(options =>
    {
        options.RoutePrefix = "swagger";
        options.SwaggerEndpoint("/openapi/v1.json", "Whale Deck API V0.1.0");
        options.DocumentTitle = "Whale Deck API";
        options.EnablePersistAuthorization();
        options.HeadContent = """
            <script>
            addEventListener('load', async () => {
              try {
                const response = await fetch('/api/v1/auth/csrf', { credentials: 'include' });
                if (!response.ok) return;
                const csrf = await response.json();
                const original = ui.getConfigs().requestInterceptor;
                ui.getConfigs().requestInterceptor = request => {
                  request.headers[csrf.headerName] = csrf.token;
                  return original ? original(request) : request;
                };
              } catch (_) { }
            });
            </script>
            """;
    });

    var openApi = app.MapOpenApi();
    if (app.Environment.IsDevelopment()) openApi.AllowAnonymous(); else openApi.RequireAuthorization("Administrator");
    app.MapHealthChecks("/health/live").AllowAnonymous();
    app.MapControllers().RequireRateLimiting("api");
    await app.RunAsync();
}
catch (Exception exception)
{
    Log.Fatal(exception, "WhaleDeck API terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}

public partial class Program;
