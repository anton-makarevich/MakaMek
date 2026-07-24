using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Sanet.MakaMek.Hub.Contracts;
using Sanet.MakaMek.Hub.Relay;
using Sanet.MakaMek.Hub.Security;
using Shouldly;

namespace Sanet.MakaMek.Hub.Tests.Relay;

public class RelayHubConnectionTests
{
    private const string GroupProbeMethod = "__relay_group_probe";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task Connect_WithValidCredentials_AttachesToBoundRoomGroupOnly()
    {
        await using var factory = new HubApplicationFactory();
        using var client = factory.CreateClient();

        var host = await CreateReadyHostAsync(client);
        var other = await CreateReadyHostAsync(client);

        await using var hostConnection = factory.CreateRelayHubConnection(
            HubApplicationFactory.ApiKey,
            host.SessionToken);
        await using var otherConnection = factory.CreateRelayHubConnection(
            HubApplicationFactory.ApiKey,
            other.SessionToken);

        var hostProbe = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var otherProbe = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        hostConnection.On<string>(GroupProbeMethod, payload => hostProbe.TrySetResult(payload));
        otherConnection.On<string>(GroupProbeMethod, payload => otherProbe.TrySetResult(payload));

        await hostConnection.StartAsync();
        await otherConnection.StartAsync();

        hostConnection.State.ShouldBe(HubConnectionState.Connected);
        otherConnection.State.ShouldBe(HubConnectionState.Connected);

        var hubContext = factory.Services.GetRequiredService<IHubContext<RelayHub>>();
        await hubContext.Clients.Group(host.RoomCode).SendAsync(GroupProbeMethod, "bound-room");

        var received = await hostProbe.Task.WaitAsync(TimeSpan.FromSeconds(5));
        received.ShouldBe("bound-room");

        var otherCompleted = await Task.WhenAny(otherProbe.Task, Task.Delay(500));
        otherCompleted.ShouldNotBe(otherProbe.Task);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-the-configured-key")]
    public async Task Connect_WithMissingOrInvalidApiKey_IsRejectedWithoutLeakingConfiguredKey(string? apiKey)
    {
        await using var factory = new HubApplicationFactory();
        using var client = factory.CreateClient();
        var host = await CreateReadyHostAsync(client);

        using var response = await PostNegotiateAsync(client, apiKey, host.SessionToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Headers.CacheControl?.NoStore.ShouldBeTrue();

        var body = await response.Content.ReadAsStringAsync();
        body.ShouldBeEmpty();
        body.ShouldNotContain(HubApplicationFactory.ApiKey);
        body.ShouldNotContain(host.SessionToken);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-valid-session-token")]
    public async Task Connect_WithMissingOrMalformedSessionToken_IsRejectedWithoutLeakingCredentials(
        string? sessionToken)
    {
        await using var factory = new HubApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await PostNegotiateAsync(client, HubApplicationFactory.ApiKey, sessionToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var body = await response.Content.ReadAsStringAsync();
        body.ShouldBeEmpty();
        body.ShouldNotContain(HubApplicationFactory.ApiKey);
        if (!string.IsNullOrEmpty(sessionToken))
        {
            body.ShouldNotContain(sessionToken);
        }
    }

    [Fact]
    public async Task Connect_WithExpiredSessionToken_IsRejectedWithoutLeakingToken()
    {
        var timeProvider = new FixedTimeProvider(new DateTimeOffset(2026, 7, 23, 12, 0, 0, TimeSpan.Zero));
        await using var factory = new HubApplicationFactory(timeProvider: timeProvider);
        using var client = factory.CreateClient();
        var host = await CreateReadyHostAsync(client);

        timeProvider.Advance(TimeSpan.FromHours(2).Add(TimeSpan.FromMinutes(1)));

        using var response = await PostNegotiateAsync(
            client,
            HubApplicationFactory.ApiKey,
            host.SessionToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var body = await response.Content.ReadAsStringAsync();
        body.ShouldBeEmpty();
        body.ShouldNotContain(HubApplicationFactory.ApiKey);
        body.ShouldNotContain(host.SessionToken);
    }

    [Fact]
    public async Task Connect_WithRevokedSessionToken_IsRejectedWithoutLeakingToken()
    {
        await using var factory = new HubApplicationFactory();
        using var client = factory.CreateClient();

        var host = await CreateReadyHostAsync(client);
        var playerId = Guid.NewGuid();
        var join = await JoinRoomAsync(client, host.RoomCode, "Grace", playerId);
        join.SessionToken.ShouldNotBeNull();

        using var removeResponse = await RemoveMemberAsync(client, host.RoomCode, host.SessionToken, playerId);
        removeResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var response = await PostNegotiateAsync(
            client,
            HubApplicationFactory.ApiKey,
            join.SessionToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var body = await response.Content.ReadAsStringAsync();
        body.ShouldBeEmpty();
        body.ShouldNotContain(HubApplicationFactory.ApiKey);
        body.ShouldNotContain(join.SessionToken!);
    }

    [Fact]
    public async Task Connect_WithRoomMismatchedClosedSessionToken_IsRejectedWithoutLeakingToken()
    {
        await using var factory = new HubApplicationFactory();
        using var client = factory.CreateClient();

        var host = await CreateReadyHostAsync(client);

        using var closeResponse = await CloseRoomAsync(client, host.RoomCode, host.SessionToken);
        closeResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var response = await PostNegotiateAsync(
            client,
            HubApplicationFactory.ApiKey,
            host.SessionToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var body = await response.Content.ReadAsStringAsync();
        body.ShouldBeEmpty();
        body.ShouldNotContain(HubApplicationFactory.ApiKey);
        body.ShouldNotContain(host.SessionToken);
    }

    [Fact]
    public async Task Invoke_RoomLifecycleMethods_FailBecauseHubExposesNoManagementRpcs()
    {
        await using var factory = new HubApplicationFactory();
        using var client = factory.CreateClient();
        var host = await CreateReadyHostAsync(client);

        await using var connection = factory.CreateRelayHubConnection(
            HubApplicationFactory.ApiKey,
            host.SessionToken);
        await connection.StartAsync();

        foreach (var methodName in new[] { "CreateRoom", "JoinRoom", "MarkReady", "CloseRoom", "RemoveMember" })
        {
            var exception = await Should.ThrowAsync<HubException>(
                async () => await connection.InvokeAsync(methodName));
            exception.Message.ShouldNotContain(HubApplicationFactory.ApiKey);
            exception.Message.ShouldNotContain(host.SessionToken);
        }
    }

    private static async Task<ReadyHost> CreateReadyHostAsync(HttpClient client)
    {
        using var createResponse = await CreateRoomAsync(client, "Ada", Guid.NewGuid());
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<CreateRoomResponse>(JsonOptions);
        created.ShouldNotBeNull();
        created.Success.ShouldBeTrue();
        created.RoomCode.ShouldNotBeNull();
        created.SessionToken.ShouldNotBeNull();

        using var readyResponse = await MarkReadyAsync(client, created.RoomCode, created.SessionToken);
        readyResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        return new ReadyHost(created.RoomCode, created.SessionToken);
    }

    private static async Task<JoinResponse> JoinRoomAsync(
        HttpClient client,
        string roomCode,
        string playerName,
        Guid playerId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/rooms/{roomCode}/join");
        request.Content = JsonContent.Create(new JoinRequest(playerName, playerId));
        request.Headers.Add(ApiKeyAuthenticationDefaults.HeaderName, HubApplicationFactory.ApiKey);

        using var response = await client.SendAsync(request);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<JoinResponse>(JsonOptions);
        result.ShouldNotBeNull();
        return result;
    }

    private static async Task<HttpResponseMessage> CreateRoomAsync(
        HttpClient client,
        string playerName,
        Guid playerId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/rooms");
        request.Content = JsonContent.Create(new CreateRoomRequest(playerName, playerId));
        request.Headers.Add(ApiKeyAuthenticationDefaults.HeaderName, HubApplicationFactory.ApiKey);
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> MarkReadyAsync(
        HttpClient client,
        string roomCode,
        string sessionToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/rooms/{roomCode}/ready");
        request.Headers.Add("Session-Token", sessionToken);
        request.Headers.Add(ApiKeyAuthenticationDefaults.HeaderName, HubApplicationFactory.ApiKey);
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> CloseRoomAsync(
        HttpClient client,
        string roomCode,
        string sessionToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/rooms/{roomCode}/close");
        request.Headers.Add("Session-Token", sessionToken);
        request.Headers.Add(ApiKeyAuthenticationDefaults.HeaderName, HubApplicationFactory.ApiKey);
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> RemoveMemberAsync(
        HttpClient client,
        string roomCode,
        string sessionToken,
        Guid playerId)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Delete,
            $"/api/rooms/{roomCode}/members/{playerId}");
        request.Headers.Add("Session-Token", sessionToken);
        request.Headers.Add(ApiKeyAuthenticationDefaults.HeaderName, HubApplicationFactory.ApiKey);
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> PostNegotiateAsync(
        HttpClient client,
        string? apiKey,
        string? sessionToken)
    {
        // Compose the path without embedding secrets into assertion messages.
        var url = HubApplicationFactory.BuildRelayHubUrl("http://localhost/", apiKey, sessionToken);
        var relative = url.Replace("http://localhost", string.Empty, StringComparison.Ordinal);
        var separator = relative.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{relative}{separator}negotiateVersion=1");
        return await client.SendAsync(request);
    }

    private sealed record ReadyHost(string RoomCode, string SessionToken);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan offset) => _now += offset;
    }
}
