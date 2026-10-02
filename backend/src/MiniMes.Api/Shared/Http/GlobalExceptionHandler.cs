using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniMes.Api.Shared.Results;
using Npgsql;

namespace MiniMes.Api.Shared.Http;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    private const string UniqueViolation = "23505";

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ProblemDetails problem;
        if (IsConcurrencyConflict(exception))
        {
            problem = new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Conflict",
                Detail = "The data was changed by someone else. Refresh and try again.",
                Extensions = { ["errorCode"] = ErrorCodes.ConcurrencyConflict }
            };
        }
        else
        {
            logger.LogError(exception, "Unhandled exception");
            problem = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An unexpected error occurred.",
                Detail = "The server could not complete the request."
            };
        }

        httpContext.Response.StatusCode = problem.Status!.Value;
        await httpContext.Response.WriteAsJsonAsync(
            problem, options: null, contentType: "application/problem+json", cancellationToken);
        return true;
    }

    private static bool IsConcurrencyConflict(Exception exception) =>
        exception is DbUpdateConcurrencyException
        || exception is PostgresException { SqlState: UniqueViolation }
        || exception is DbUpdateException { InnerException: PostgresException { SqlState: UniqueViolation } };
}
