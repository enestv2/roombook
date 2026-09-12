using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Roombook.Api;

public sealed class ApiExceptionHandler(
    ILogger<ApiExceptionHandler> logger,
    IProblemDetailsService problemDetails)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var correlationId = httpContext.Items[CorrelationIdMiddleware.ItemKey]?.ToString()
            ?? httpContext.TraceIdentifier;
        logger.LogError(exception, "Unhandled request failure. CorrelationId: {CorrelationId}", correlationId);
        if (httpContext.Response.HasStarted)
            return false;

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await problemDetails.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An unexpected error occurred.",
                Detail = "The request could not be completed. Use the correlation ID when contacting support."
            }
        });
        return true;
    }
}
