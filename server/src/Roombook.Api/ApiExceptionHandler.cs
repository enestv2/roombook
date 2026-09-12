using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Roombook.Api.Localization;

namespace Roombook.Api;

public sealed class ApiExceptionHandler(
    ILogger<ApiExceptionHandler> logger,
    IProblemDetailsService problemDetails,
    IStringLocalizer<ApiMessages> messages)
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
                Title = messages[ApiErrorCodes.Unexpected].Value,
                Detail = messages["request.unexpected_detail"].Value
            }
        });
        // The shared ProblemDetails customizer adds the stable code and correlation ID.
        return true;
    }
}
