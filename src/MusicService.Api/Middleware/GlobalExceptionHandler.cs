using Microsoft.AspNetCore.Diagnostics;
using MusicService.Domain.Exceptions;

namespace MusicService.Api.Middleware;

// Maps domain exceptions to 400/404/409 and anything else to a ProblemDetails 500 response.
public class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title, detail) = exception switch
        {
            MusicValidationException e => (StatusCodes.Status400BadRequest, "Invalid request.", e.Message),
            MusicNotFoundException e => (StatusCodes.Status404NotFound, "Not found.", e.Message),
            MusicConflictException e => (StatusCodes.Status409Conflict, "Conflict.", e.Message),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.", null)
        };

        if (status == StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "Unhandled exception for {Method} {Path}",
                httpContext.Request.Method, httpContext.Request.Path);

        httpContext.Response.StatusCode = status;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails =
            {
                Title = title,
                Detail = detail,
                Status = status
            }
        });
    }
}
