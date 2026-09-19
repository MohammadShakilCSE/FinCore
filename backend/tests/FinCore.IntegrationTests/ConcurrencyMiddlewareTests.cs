using FinCore.Api.Middleware;
using FinCore.Application.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;

namespace FinCore.IntegrationTests;

public sealed class ConcurrencyMiddlewareTests
{
    [Fact]
    public async Task Conflict_returns_safe_409_without_inner_exception_details()
    {
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new ConcurrencyConflictException(new Exception("private database details")),
            NullLogger<ExceptionHandlingMiddleware>.Instance);
        var context = new DefaultHttpContext();
        await using var body = new MemoryStream();
        context.Response.Body = body;
        await middleware.InvokeAsync(context);
        Assert.Equal(409, context.Response.StatusCode);
        Assert.Equal("application/problem+json", context.Response.ContentType);
        body.Position = 0;
        using var json = await JsonDocument.ParseAsync(body);
        Assert.Equal(409, json.RootElement.GetProperty("status").GetInt32());
        Assert.Contains("refresh", json.RootElement.GetProperty("detail").GetString());
        Assert.DoesNotContain("private database details", json.RootElement.GetRawText());
    }
}
