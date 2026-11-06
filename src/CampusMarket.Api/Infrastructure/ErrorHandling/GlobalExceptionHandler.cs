using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CampusMarket.Api.Infrastructure.ErrorHandling;

// Turns every exception that no handler caught into a ProblemDetails response.
// Expected errors never get here: handlers return them as Result.
public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, code, detail) = exception switch
        {
            DbUpdateConcurrencyException => (
                StatusCodes.Status409Conflict,
                "Concurrency.Conflict",
                "The data was changed by another request at the same time. Load it again and retry."),
            BadHttpRequestException badRequest => (
                badRequest.StatusCode,
                "Request.Invalid",
                "The request is not valid. Check the JSON body, the route and the query parameters."),
            _ => (
                StatusCodes.Status500InternalServerError,
                "Server.Error",
                "An unexpected error occurred.")
        };

        if (status >= StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "Unhandled exception on {Path}", httpContext.Request.Path);
        else
            logger.LogWarning(exception, "Request on {Path} failed with {Status}", httpContext.Request.Path, status);

        httpContext.Response.StatusCode = status;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Detail = detail,
                Extensions = { ["code"] = code }
            }
        });
    }
}