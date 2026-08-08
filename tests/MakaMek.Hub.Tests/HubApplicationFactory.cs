using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Sanet.MakaMek.Hub.Security;

namespace Sanet.MakaMek.Hub.Tests;

public sealed class HubApplicationFactory : WebApplicationFactory<Program>
{
    public const string ApiKey = "test-api-key";

    private readonly int _maxConcurrentRooms;
    private readonly int _joinRateLimitPerMinute;
    private readonly int _relayRateLimitPerMinute;
    private readonly int _maxRelayPayloadBytes;
    private readonly int _roomTtlSeconds;
    private readonly int _dissolutionGracePeriodSeconds;
    private readonly TimeProvider? _timeProvider;

    public HubApplicationFactory(
        int maxConcurrentRooms = 10,
        int joinRateLimitPerMinute = 100,
        int relayRateLimitPerMinute = 1000,
        int maxRelayPayloadBytes = 256 * 1024,
        int roomTtlSeconds = 7200,
        int dissolutionGracePeriodSeconds = 30,
        TimeProvider? timeProvider = null)
    {
        _maxConcurrentRooms = maxConcurrentRooms;
        _joinRateLimitPerMinute = joinRateLimitPerMinute;
        _relayRateLimitPerMinute = relayRateLimitPerMinute;
        _maxRelayPayloadBytes = maxRelayPayloadBytes;
        _roomTtlSeconds = roomTtlSeconds;
        _dissolutionGracePeriodSeconds = dissolutionGracePeriodSeconds;
        _timeProvider = timeProvider;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Hub:ApiKey"] = ApiKey,
                ["Hub:MaxConcurrentRooms"] = _maxConcurrentRooms.ToString(),
                ["Hub:JoinRateLimitPerMinute"] = _joinRateLimitPerMinute.ToString(),
                ["Hub:RelayRateLimitPerMinute"] = _relayRateLimitPerMinute.ToString(),
                ["Hub:MaxRelayPayloadBytes"] = _maxRelayPayloadBytes.ToString(),
                ["Hub:RoomTtlSeconds"] = _roomTtlSeconds.ToString(),
                ["Hub:DissolutionGracePeriodSeconds"] = _dissolutionGracePeriodSeconds.ToString()
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.PostConfigure<HubOptions>(options =>
            {
                options.KeepAliveInterval = TimeSpan.FromDays(1);
                options.ClientTimeoutInterval = TimeSpan.FromDays(2);
            });

            if (_timeProvider is not null)
            {
                var existing = services.Where(descriptor => descriptor.ServiceType == typeof(TimeProvider)).ToList();
                foreach (var descriptor in existing)
                {
                    services.Remove(descriptor);
                }

                services.AddSingleton(_timeProvider);
            }
        });
    }

    public HubConnection CreateRelayHubConnection(string? apiKey, string? sessionToken)
    {
        var url = BuildRelayHubUrl(Server.BaseAddress.ToString(), apiKey, sessionToken);

        return new HubConnectionBuilder()
            .WithUrl(url, options =>
            {
                options.Transports = HttpTransportType.WebSockets;
                options.HttpMessageHandlerFactory = _ => Server.CreateHandler();
                options.WebSocketFactory = async (context, cancellationToken) =>
                {
                    var webSocketClient = Server.CreateWebSocketClient();
                    return await webSocketClient.ConnectAsync(context.Uri, cancellationToken);
                };
            })
            .WithKeepAliveInterval(TimeSpan.FromDays(1))
            .WithServerTimeout(TimeSpan.FromDays(2))
            .Build();
    }

    public static string BuildRelayHubUrl(string baseAddress, string? apiKey, string? sessionToken)
    {
        var builder = new UriBuilder(new Uri(new Uri(baseAddress), RelayAuthenticationDefaults.HubPath));
        var queryParts = new List<string>();

        if (apiKey is not null)
        {
            queryParts.Add(
                $"{ApiKeyAuthenticationDefaults.ApiKeyQueryParameterName}={Uri.EscapeDataString(apiKey)}");
        }

        if (sessionToken is not null)
        {
            queryParts.Add(
                $"{ApiKeyAuthenticationDefaults.SessionTokenQueryParameterName}={Uri.EscapeDataString(sessionToken)}");
        }

        builder.Query = string.Join('&', queryParts);
        return builder.Uri.AbsoluteUri;
    }
}
