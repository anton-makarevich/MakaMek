using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;

namespace Sanet.MakaMek.Core.Tests.Services.Transport.Relay;

/// <summary>
/// Hub that stands in for the remote relay hub while exercising the real
/// SignalR WebSocket handshake, rejecting connections whose apiKey query
/// parameter does not match the configured key.
/// </summary>
public sealed class TestRelayHub : Hub
{
}

internal static class TestRelayHubHost
{
    public static async Task<WebApplication> StartAsync(string requiredApiKey)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSignalR();
        var app = builder.Build();
        app.UseWebSockets();
        app.Use(async (context, next) =>
        {
            if (context.Request.Query["apiKey"].ToString() != requiredApiKey)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            await next(context);
        });
        app.MapHub<TestRelayHub>("/hubs/relay");
        await app.StartAsync();
        return app;
    }
}
