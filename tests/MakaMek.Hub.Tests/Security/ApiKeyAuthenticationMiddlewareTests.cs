using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sanet.MakaMek.Hub.Configuration;
using Sanet.MakaMek.Hub.Security;
using Sanet.MakaMek.Hub.Tests.TestLoggers;
using Shouldly;

namespace Sanet.MakaMek.Hub.Tests.Security;

public class ApiKeyAuthenticationMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WhenNoApiKeyConfigured_ReturnsUnauthorizedWithoutCallingNext()
    {
        var logger = new CapturingLogger<ApiKeyAuthenticationMiddleware>();
        var nextCalled = false;
        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };
        var middleware = new ApiKeyAuthenticationMiddleware(next);
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/api/rooms";

        await middleware.InvokeAsync(
            context,
            Options.Create(new HubOptions { ApiKey = string.Empty }),
            logger);

        context.Response.StatusCode.ShouldBe(StatusCodes.Status401Unauthorized);
        context.Response.Headers.CacheControl.ToString().ShouldBe("no-store");
        nextCalled.ShouldBeFalse();
        logger.GetMessages(LogLevel.Warning).ShouldContain(
            message => message.Contains("no API key is configured", StringComparison.Ordinal));
    }

    [Fact]
    public async Task InvokeAsync_WithValidApiKey_CallsNext()
    {
        var logger = new CapturingLogger<ApiKeyAuthenticationMiddleware>();
        var nextCalled = false;
        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };
        var middleware = new ApiKeyAuthenticationMiddleware(next);
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/api/rooms";
        context.Request.Headers[ApiKeyAuthenticationDefaults.HeaderName] = "secret";

        await middleware.InvokeAsync(
            context,
            Options.Create(new HubOptions { ApiKey = "secret" }),
            logger);

        nextCalled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe(StatusCodes.Status200OK);
    }
}
