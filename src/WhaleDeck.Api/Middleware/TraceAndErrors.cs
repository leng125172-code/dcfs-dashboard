using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace WhaleDeck.Api.Middleware;

public static class TraceAndErrors
{
    public static async Task WriteProblemAsync(HttpContext context)
    {
        var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
        var (status, code, title) = exception switch
        {
            UnauthorizedAccessException => (StatusCodes.Status403Forbidden, "AUTH_FORBIDDEN", "The operation is not permitted."),
            KeyNotFoundException => (StatusCodes.Status404NotFound, "RESOURCE_NOT_FOUND", "The requested resource was not found."),
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "CONCURRENCY_CONFLICT", "The resource was changed by another request."),
            ArgumentException => (StatusCodes.Status400BadRequest, "VALIDATION_FAILED", "The request is invalid."),
            InvalidOperationException => (StatusCodes.Status409Conflict, "OPERATION_CONFLICT", "The operation cannot be completed in the current state."),
            _ => (StatusCodes.Status500InternalServerError, "INTERNAL_ERROR", "An internal error occurred.")
        };
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = status,
            Title = title,
            Extensions = { ["code"] = code, ["traceId"] = context.TraceIdentifier }
        });
    }
}
