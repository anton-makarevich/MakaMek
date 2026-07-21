using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Sanet.MakaMek.Hub.Configuration;

namespace Sanet.MakaMek.Hub.Security;

/// <summary>
/// Rejects unauthenticated REST requests without logging or returning the configured API key.
/// </summary>
public sealed class ApiKeyAuthenticationMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IOptions<HubOptions> options)
    {
        if (!context.Request.Path.StartsWithSegments("/api"))
        {
            await next(context);
            return;
        }

        var configuredApiKey = options.Value.ApiKey;
        var suppliedApiKey = context.Request.Headers[ApiKeyAuthenticationDefaults.HeaderName];

        if (string.IsNullOrWhiteSpace(configuredApiKey) || !IsMatch(configuredApiKey, suppliedApiKey))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.Headers.CacheControl = "no-store";
            return;
        }

        await next(context);
    }

    private static bool IsMatch(string expectedApiKey, StringValues suppliedApiKey)
    {
        if (suppliedApiKey.Count != 1 || string.IsNullOrEmpty(suppliedApiKey[0]))
        {
            return false;
        }

        var expectedBytes = Encoding.UTF8.GetBytes(expectedApiKey);
        var suppliedBytes = Encoding.UTF8.GetBytes(suppliedApiKey[0]!);

        return CryptographicOperations.FixedTimeEquals(expectedBytes, suppliedBytes);
    }
}
