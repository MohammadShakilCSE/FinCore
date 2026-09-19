using FinCore.Api.Middleware;
using FinCore.Application.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;

namespace FinCore.IntegrationTests;

public sealed class IdempotencyMiddlewareTests
{
    [Theory]
    [InlineData("conflict", 409)]
    [InlineData("processing", 409)]
    [InlineData("unauthenticated", 401)]
    [InlineData("forbidden", 403)]
    public async Task Expected_errors_are_safe_and_processing_is_retryable(string type, int status)
    {
        Exception error = type switch
        {
            "conflict" => new IdempotencyConflictException(),
            "processing" => new IdempotencyInProgressException(new Exception("secret database internals")),
            "unauthenticated" => new AuthenticationRequiredException(),
            _ => new WalletAccessDeniedException()
        };
        var middleware = new ExceptionHandlingMiddleware(_ => throw error, NullLogger<ExceptionHandlingMiddleware>.Instance);
        var context = new DefaultHttpContext();
        await using var body = new MemoryStream();
        context.Response.Body = body;
        await middleware.InvokeAsync(context);
        Assert.Equal(status, context.Response.StatusCode);
        Assert.Equal("application/problem+json", context.Response.ContentType);
        Assert.Equal(type == "processing" ? "2" : "", context.Response.Headers.RetryAfter.ToString());
        body.Position = 0;
        using var payload = await JsonDocument.ParseAsync(body);
        Assert.DoesNotContain("secret database internals", payload.RootElement.GetRawText());
    }
}
