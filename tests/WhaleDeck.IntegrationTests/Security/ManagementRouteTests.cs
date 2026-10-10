using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using WhaleDeck.Api.Controllers;
using WhaleDeck.Api.Security;

namespace WhaleDeck.IntegrationTests.Security;

public sealed class ManagementRouteTests
{
    [Fact]
    public void OperationRoutesDoNotUseMvcReservedActionKey()
    {
        var templates = typeof(ManagementController)
            .GetMethods()
            .SelectMany(method => method.GetCustomAttributes(typeof(HttpPostAttribute), inherit: true))
            .Cast<HttpPostAttribute>()
            .Select(attribute => attribute.Template ?? string.Empty)
            .ToArray();

        Assert.DoesNotContain(templates, template => template.Contains("{action}", StringComparison.Ordinal));
        Assert.Contains(templates, template => template.EndsWith("/{operation}/plan", StringComparison.Ordinal));
        Assert.Contains(templates, template => template.EndsWith("/{operation}", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("/api/v1/auth/me", true)]
    [InlineData("/api/v1/containers", true)]
    [InlineData("/api/v1/auth/login", false)]
    [InlineData("/swagger", false)]
    public void AnonymousApiFetchesDoNotRedirectAcrossOrigins(string path, bool expected)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;

        Assert.Equal(expected, OidcChallengeBehavior.ShouldReturnUnauthorized(context.Request));
    }
}
