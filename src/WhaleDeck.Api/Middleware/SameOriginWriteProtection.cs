using Microsoft.AspNetCore.Mvc;

namespace WhaleDeck.Api.Middleware;

public static class SameOriginWriteProtection
{
    private static readonly HashSet<string> SafeMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        HttpMethods.Get,
        HttpMethods.Head,
        HttpMethods.Options,
        HttpMethods.Trace
    };

    public static async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (!RequiresValidation(context.Request) || IsSameOrigin(context.Request))
        {
            await next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden,
            Title = "The request origin is not permitted.",
            Extensions =
            {
                ["code"] = "ORIGIN_MISMATCH",
                ["traceId"] = context.TraceIdentifier
            }
        });
    }

    public static bool RequiresValidation(HttpRequest request) =>
        request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase) &&
        !SafeMethods.Contains(request.Method);

    public static bool IsSameOrigin(HttpRequest request)
    {
        if (request.Headers.Origin.Count != 1 ||
            !Uri.TryCreate(request.Headers.Origin[0], UriKind.Absolute, out var origin) ||
            !string.IsNullOrEmpty(origin.UserInfo) ||
            !string.IsNullOrEmpty(origin.Query) ||
            !string.IsNullOrEmpty(origin.Fragment) ||
            origin.AbsolutePath != "/")
        {
            return false;
        }

        var expected = new UriBuilder(request.Scheme, request.Host.Host, request.Host.Port ?? -1).Uri;
        return Uri.Compare(origin, expected, UriComponents.SchemeAndServer,
            UriFormat.Unescaped, StringComparison.OrdinalIgnoreCase) == 0;
    }
}
