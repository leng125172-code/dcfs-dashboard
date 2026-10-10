using Microsoft.AspNetCore.Http;
using WhaleDeck.Api.Middleware;

namespace WhaleDeck.IntegrationTests.Security;

public sealed class SameOriginWriteProtectionTests
{
    [Theory]
    [InlineData("GET", "/api/v1/containers", false)]
    [InlineData("POST", "/signin-oidc/external", false)]
    [InlineData("POST", "/api/v1/auth/logout", true)]
    public void ValidationIsLimitedToUnsafeApiRequests(string method, string path, bool expected)
    {
        var request = NewRequest(method, path, "http://192.168.100.13:8080");

        Assert.Equal(expected, SameOriginWriteProtection.RequiresValidation(request));
    }

    [Theory]
    [InlineData("http://192.168.100.13:8080", true)]
    [InlineData("http://192.168.100.13:8081", false)]
    [InlineData("http://attacker.invalid", false)]
    [InlineData("null", false)]
    [InlineData(null, false)]
    public void OriginMustMatchForwardedRequestOriginExactly(string? origin, bool expected)
    {
        var request = NewRequest("POST", "/api/v1/auth/logout", origin);

        Assert.Equal(expected, SameOriginWriteProtection.IsSameOrigin(request));
    }

    private static HttpRequest NewRequest(string method, string path, string? origin)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Request.Scheme = "http";
        context.Request.Host = new HostString("192.168.100.13", 8080);
        if (origin is not null) context.Request.Headers.Origin = origin;
        return context.Request;
    }
}
