using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Sanet.MakaMek.Hub.Configuration;
using Sanet.MakaMek.Hub.Rooms;

namespace Sanet.MakaMek.Hub.Security;

/// <summary>
/// Validates query-string API key and session token for the SignalR relay hub path
/// before the WebSocket upgrade completes.
/// </summary>
public sealed class RelayAuthenticationMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext context,
        IOptions<HubOptions> options,
        IRoomManager roomManager)
    {
        if (!context.Request.Path.StartsWithSegments(RelayAuthenticationDefaults.HubPath))
        {
            await next(context);
            return;
        }

        var configuredApiKey = options.Value.ApiKey;
        var suppliedApiKey = context.Request.Query[ApiKeyAuthenticationDefaults.ApiKeyQueryParameterName];

        if (string.IsNullOrWhiteSpace(configuredApiKey) || !IsMatch(configuredApiKey, suppliedApiKey))
        {
            RejectUnauthorized(context);
            return;
        }

        var suppliedSessionToken =
            context.Request.Query[ApiKeyAuthenticationDefaults.SessionTokenQueryParameterName];

        if (suppliedSessionToken.Count != 1 || string.IsNullOrWhiteSpace(suppliedSessionToken[0]))
        {
            RejectUnauthorized(context);
            return;
        }

        var session = roomManager.AuthenticateSession(suppliedSessionToken[0]!);
        if (session is null)
        {
            RejectUnauthorized(context);
            return;
        }

        context.Items[RelayAuthenticationDefaults.AuthenticatedSessionItemKey] = session;
        await next(context);
    }

    private static void RejectUnauthorized(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.Headers.CacheControl = "no-store";
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
