using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;

namespace Sanet.MakaMek.Core.Tests.Services.Transport.Relay;

/// <summary>
/// Hub that accepts any connection, standing in for the remote relay hub
/// while exercising the real SignalR WebSocket handshake.
/// </summary>
public sealed class TestRelayHub : Hub
{
}

internal static class TestRelayHubHost
{
    public static async Task<WebApplication> StartAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSignalR();
        var app = builder.Build();
        app.UseWebSockets();
        app.MapHub<TestRelayHub>("/hubs/relay");
        await app.StartAsync();
        return app;
    }
}
