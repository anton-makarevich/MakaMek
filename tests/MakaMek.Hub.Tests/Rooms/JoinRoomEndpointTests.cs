using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Sanet.MakaMek.Hub.Contracts;
using Sanet.MakaMek.Hub.Security;
using Shouldly;

namespace Sanet.MakaMek.Hub.Tests.Rooms;

public class JoinRoomEndpointTests
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    [Fact]
    public async Task JoinRoom_ReadyRoom_ReturnsDeviceSessionAndHostGameId()
    {
        await using var factory = new HubApplicationFactory();
        using var client = factory.CreateClient();
        var hostGameId = Guid.NewGuid();

        using var createResponse = await CreateRoomAsync(client, hostGameId, HubApplicationFactory.ApiKey);
        var createResult = await createResponse.Content.ReadFromJsonAsync<CreateRoomResponse>(JsonOptions);
        var roomCode = createResult!.RoomCode!;

        await MarkReadyAsync(client, roomCode, createResult.SessionToken!, HubApplicationFactory.ApiKey);

        using var joinResponse = await JoinRoomAsync(client, roomCode, sessionToken: null, HubApplicationFactory.ApiKey);

        joinResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var result = await joinResponse.Content.ReadFromJsonAsync<JoinResponse>(JsonOptions);
        result.ShouldNotBeNull();
        result.Success.ShouldBeTrue();
        result.Role.ShouldBe("Client");
        result.DeviceSessionId.ShouldNotBeNull();
        result.DeviceSessionId.ShouldNotBe(Guid.Empty);
        result.HostGameId.ShouldBe(hostGameId);
        string.IsNullOrWhiteSpace(result.SessionToken).ShouldBeFalse();
        result.Error.ShouldBeNull();
    }

    [Fact]
    public async Task JoinRoom_MissingRoom_ReturnsNotFound()
    {
        await using var factory = new HubApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await JoinRoomAsync(client, "NOEXIST", sessionToken: null, HubApplicationFactory.ApiKey);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var result = await response.Content.ReadFromJsonAsync<JoinResponse>(JsonOptions);
        result.ShouldNotBeNull();
        result.Success.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
        result.Error!.Code.ShouldBe(HubErrorCode.RoomNotFound);
    }

    [Fact]
    public async Task JoinRoom_NotReadyRoom_ReturnsConflict()
    {
        await using var factory = new HubApplicationFactory();
        using var client = factory.CreateClient();

        using var createResponse = await CreateRoomAsync(client, Guid.NewGuid(), HubApplicationFactory.ApiKey);
        var createResult = await createResponse.Content.ReadFromJsonAsync<CreateRoomResponse>(JsonOptions);
        var roomCode = createResult!.RoomCode!;

        using var joinResponse = await JoinRoomAsync(client, roomCode, sessionToken: null, HubApplicationFactory.ApiKey);

        joinResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var result = await joinResponse.Content.ReadFromJsonAsync<JoinResponse>(JsonOptions);
        result.ShouldNotBeNull();
        result.Success.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
        result.Error!.Code.ShouldBe(HubErrorCode.HostNotReady);
    }

    [Fact]
    public async Task JoinRoom_RejoinWithValidSessionToken_ReusesDeviceSession()
    {
        await using var factory = new HubApplicationFactory();
        using var client = factory.CreateClient();
        var hostGameId = Guid.NewGuid();

        using var createResponse = await CreateRoomAsync(client, hostGameId, HubApplicationFactory.ApiKey);
        var createResult = await createResponse.Content.ReadFromJsonAsync<CreateRoomResponse>(JsonOptions);
        var roomCode = createResult!.RoomCode!;

        await MarkReadyAsync(client, roomCode, createResult.SessionToken!, HubApplicationFactory.ApiKey);

        using var firstJoin = await JoinRoomAsync(client, roomCode, sessionToken: null, HubApplicationFactory.ApiKey);
        var first = await firstJoin.Content.ReadFromJsonAsync<JoinResponse>(JsonOptions);
        first!.DeviceSessionId.ShouldNotBeNull();

        using var rejoin = await JoinRoomAsync(client, roomCode, first.SessionToken, HubApplicationFactory.ApiKey);

        rejoin.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await rejoin.Content.ReadFromJsonAsync<JoinResponse>(JsonOptions);
        result.ShouldNotBeNull();
        result.Success.ShouldBeTrue();
        result.DeviceSessionId.ShouldBe(first.DeviceSessionId);
        result.HostGameId.ShouldBe(hostGameId);
    }

    [Fact]
    public async Task JoinRoom_TwoNewDevices_EachGetDistinctDeviceSessions()
    {
        await using var factory = new HubApplicationFactory();
        using var client = factory.CreateClient();
        var hostGameId = Guid.NewGuid();

        using var createResponse = await CreateRoomAsync(client, hostGameId, HubApplicationFactory.ApiKey);
        var createResult = await createResponse.Content.ReadFromJsonAsync<CreateRoomResponse>(JsonOptions);
        var roomCode = createResult!.RoomCode!;

        await MarkReadyAsync(client, roomCode, createResult.SessionToken!, HubApplicationFactory.ApiKey);

        using var join1 = await JoinRoomAsync(client, roomCode, sessionToken: null, HubApplicationFactory.ApiKey);
        using var join2 = await JoinRoomAsync(client, roomCode, sessionToken: null, HubApplicationFactory.ApiKey);

        join1.StatusCode.ShouldBe(HttpStatusCode.OK);
        join2.StatusCode.ShouldBe(HttpStatusCode.OK);

        var first = await join1.Content.ReadFromJsonAsync<JoinResponse>(JsonOptions);
        var second = await join2.Content.ReadFromJsonAsync<JoinResponse>(JsonOptions);
        first!.DeviceSessionId.ShouldNotBe(second!.DeviceSessionId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-the-configured-key")]
    public async Task JoinRoom_WithMissingOrInvalidApiKey_IsRejectedWithoutLeakingConfiguredKey(string? apiKey)
    {
        await using var factory = new HubApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await JoinRoomAsync(client, "ABC234", sessionToken: null, apiKey);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldNotContain(HubApplicationFactory.ApiKey);
    }

    [Fact]
    public async Task JoinRoom_ExceedsRateLimit_Returns429()
    {
        await using var factory = new HubApplicationFactory(joinRateLimitPerMinute: 2);
        using var client = factory.CreateClient();

        var hostGameId = Guid.NewGuid();
        using var createResponse = await CreateRoomAsync(client, hostGameId, HubApplicationFactory.ApiKey);
        var createResult = await createResponse.Content.ReadFromJsonAsync<CreateRoomResponse>(JsonOptions);
        var roomCode = createResult!.RoomCode!;
        await MarkReadyAsync(client, roomCode, createResult.SessionToken!, HubApplicationFactory.ApiKey);

        using var r1 = await JoinRoomAsync(client, roomCode, sessionToken: null, HubApplicationFactory.ApiKey);
        using var r2 = await JoinRoomAsync(client, roomCode, sessionToken: null, HubApplicationFactory.ApiKey);
        using var r3 = await JoinRoomAsync(client, roomCode, sessionToken: null, HubApplicationFactory.ApiKey);

        r3.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task MarkRoomReady_WithHostSession_ReturnsOk()
    {
        await using var factory = new HubApplicationFactory();
        using var client = factory.CreateClient();
        var hostGameId = Guid.NewGuid();

        using var createResponse = await CreateRoomAsync(client, hostGameId, HubApplicationFactory.ApiKey);
        var createResult = await createResponse.Content.ReadFromJsonAsync<CreateRoomResponse>(JsonOptions);
        var roomCode = createResult!.RoomCode!;

        using var response = await MarkReadyAsync(client, roomCode, createResult.SessionToken!, HubApplicationFactory.ApiKey);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ReadyResponse>(JsonOptions);
        result.ShouldNotBeNull();
        result.Success.ShouldBeTrue();
        result.Error.ShouldBeNull();
    }

    [Fact]
    public async Task MarkRoomReady_NonHost_ReturnsConflict()
    {
        await using var factory = new HubApplicationFactory();
        using var client = factory.CreateClient();

        using var createResponse = await CreateRoomAsync(client, Guid.NewGuid(), HubApplicationFactory.ApiKey);
        var createResult = await createResponse.Content.ReadFromJsonAsync<CreateRoomResponse>(JsonOptions);
        var roomCode = createResult!.RoomCode!;

        using var response = await MarkReadyAsync(client, roomCode, "not-the-host-token", HubApplicationFactory.ApiKey);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var result = await response.Content.ReadFromJsonAsync<ReadyResponse>(JsonOptions);
        result.ShouldNotBeNull();
        result.Success.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
        result.Error!.Code.ShouldBe(HubErrorCode.NotHost);
    }

    [Fact]
    public async Task MarkRoomReady_MissingRoom_ReturnsNotFound()
    {
        await using var factory = new HubApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await MarkReadyAsync(client, "NOEXIST", "any-token", HubApplicationFactory.ApiKey);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var result = await response.Content.ReadFromJsonAsync<ReadyResponse>(JsonOptions);
        result.ShouldNotBeNull();
        result.Success.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
        result.Error!.Code.ShouldBe(HubErrorCode.RoomNotFound);
    }

    private static async Task<HttpResponseMessage> CreateRoomAsync(
        HttpClient client,
        Guid gameId,
        string? apiKey)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/rooms");
        request.Content = JsonContent.Create(new CreateRoomRequest(gameId));

        if (apiKey is not null)
        {
            request.Headers.Add(ApiKeyAuthenticationDefaults.HeaderName, apiKey);
        }

        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> JoinRoomAsync(
        HttpClient client,
        string roomCode,
        string? sessionToken,
        string? apiKey)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/rooms/{roomCode}/join");
        if (sessionToken is not null)
        {
            request.Headers.Add("Session-Token", sessionToken);
        }

        if (apiKey is not null)
        {
            request.Headers.Add(ApiKeyAuthenticationDefaults.HeaderName, apiKey);
        }

        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> MarkReadyAsync(
        HttpClient client,
        string roomCode,
        string sessionToken,
        string? apiKey)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/rooms/{roomCode}/ready");
        request.Headers.Add("Session-Token", sessionToken);

        if (apiKey is not null)
        {
            request.Headers.Add(ApiKeyAuthenticationDefaults.HeaderName, apiKey);
        }

        return await client.SendAsync(request);
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
