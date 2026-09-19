using FinCore.Domain.Exceptions;
using System.Net;
using System.Text.Json;

namespace FinCore.Api.Middleware;

public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (DomainException exception)
        {
            await HandleDomainExceptionAsync(
                context,
                exception);
        }
        catch (Exception exception)
        {
            await HandleUnexpectedExceptionAsync(
                context,
                exception);
        }
    }

    private static async Task HandleDomainExceptionAsync(
        HttpContext context,
        DomainException exception)
    {
        context.Response.StatusCode =
            StatusCodes.Status400BadRequest;

        context.Response.ContentType =
            "application/problem+json";

        var response = new
        {
            status = StatusCodes.Status400BadRequest,
            title = "Business rule violation",
            detail = exception.Message
        };

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(response));
    }

    private async Task HandleUnexpectedExceptionAsync(
        HttpContext context,
        Exception exception)
    {
        _logger.LogError(
            exception,
            "Unhandled exception occurred.");

        context.Response.StatusCode =
            StatusCodes.Status500InternalServerError;

        context.Response.ContentType =
            "application/problem+json";

        var response = new
        {
            status = StatusCodes.Status500InternalServerError,
            title = "An unexpected error occurred.",
            detail = "Something went wrong while processing the request."
        };

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(response));
    }
}