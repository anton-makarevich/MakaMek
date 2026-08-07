using System.Net;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using Sanet.MakaMek.Hub.Configuration;
using Sanet.MakaMek.Hub.Contracts;
using Sanet.MakaMek.Hub.Relay;
using Sanet.MakaMek.Hub.Rooms;
using Sanet.MakaMek.Hub.Security;

var builder = WebApplication.CreateBuilder(args);

// Single-line console output with timestamps so room lifecycle, connections and relay
// traffic are easy to follow while debugging the relay locally.
builder.Logging.AddSimpleConsole(options =>
{
    options.TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff ";
    options.SingleLine = true;
});

builder.Services
    .AddOptions<HubOptions>()
    .Bind(builder.Configuration.GetSection(HubOptions.SectionName))
    .Validate(
        options => options.MaxConcurrentRooms > 0,
        $"{HubOptions.SectionName}:MaxConcurrentRooms must be greater than zero.")
    .Validate(
        options => options.JoinRateLimitPerMinute > 0,
        $"{HubOptions.SectionName}:JoinRateLimitPerMinute must be greater than zero.")
    .Validate(
        options => options.RelayRateLimitPerMinute > 0,
        $"{HubOptions.SectionName}:RelayRateLimitPerMinute must be greater than zero.")
    .Validate(
        options => options.MaxRelayPayloadBytes > 0,
        $"{HubOptions.SectionName}:MaxRelayPayloadBytes must be greater than zero.")
    .Validate(
        options => options.RoomTtlSeconds > 0,
        $"{HubOptions.SectionName}:RoomTtlSeconds must be greater than zero.")
    .Validate(
        options => options.DissolutionGracePeriodSeconds > 0,
        $"{HubOptions.SectionName}:DissolutionGracePeriodSeconds must be greater than zero.")
    .ValidateOnStart();

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("JoinRateLimit", httpContext =>
    {
        var hubOptions = httpContext.RequestServices.GetRequiredService<IOptions<HubOptions>>().Value;
        var partitionKey = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = hubOptions.JoinRateLimitPerMinute,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });

    options.OnRejected = async (context, cancellationToken) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                ((int)retryAfter.TotalSeconds).ToString();
        }

        context.HttpContext.Response.ContentType = "application/problem+json";
        await context.HttpContext.Response.WriteAsJsonAsync(
            new JoinResponse(
                Success: false,
                Role: null,
                DeviceSessionId: null,
                HostGameId: null,
                SessionToken: null,
                Error: new HubError(HubErrorCode.RateLimited, "Too many join attempts. Please try again later.")),
            cancellationToken);
    };
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

    var trustedProxies = builder.Configuration
        .GetSection($"{HubOptions.SectionName}:TrustedProxies")
        .Get<string[]>() ?? [];

    foreach (var proxy in trustedProxies)
    {
        if (proxy.Contains('/'))
        {
            options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(proxy));
        }
        else
        {
            options.KnownProxies.Add(IPAddress.Parse(proxy));
        }
    }
});

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IRoomCodeGenerator, CryptographicRoomCodeGenerator>();
builder.Services.AddSingleton<IRoomManager, RoomManager>();
builder.Services.AddSingleton<IRelayRateLimiter, RelayRateLimiter>();
builder.Services.AddSignalR(options =>
{
    var maxPayload = builder.Configuration.GetValue(
        $"{HubOptions.SectionName}:MaxRelayPayloadBytes",
        256 * 1024);
    options.MaximumReceiveMessageSize = maxPayload + RelayHub.ReceiveMessageSizeOverheadBytes;
});

var app = builder.Build();

app.UseForwardedHeaders();
app.UseRateLimiter();
app.UseMiddleware<ApiKeyAuthenticationMiddleware>();
app.UseMiddleware<RelayAuthenticationMiddleware>();
app.MapControllers();
app.MapGet("/health", () =>
{
    var version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown";
    return Results.Ok(new
    {
        status = "healthy",
        service = "MakaMek.Hub",
        version
    });
});

app.MapHub<RelayHub>(RelayAuthenticationDefaults.HubPath, options =>
{
    options.Transports = HttpTransportType.WebSockets;
});

app.Run();

public partial class Program;
