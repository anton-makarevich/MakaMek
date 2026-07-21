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
    public async Task JoinRoom_ReadyRoom_ReturnsSessionTokenAndHostIdentity()
    {
        await using var factory = new HubApplicationFactory();
        using var client = factory.CreateClient();
        var hostId = Guid.NewGuid();

        using var createResponse = await CreateRoomAsync(client, "Ada", hostId, HubApplicationFactory.ApiKey);
        var createResult = await createResponse.Content.ReadFromJsonAsync<CreateRoomResponse>(JsonOptions);
        var roomCode = createResult!.RoomCode!;

        await MarkReadyAsync(client, roomCode, hostId, HubApplicationFactory.ApiKey);

        var playerId = Guid.NewGuid();
        using var joinResponse = await JoinRoomAsync(client, roomCode, "Grace", playerId, HubApplicationFactory.ApiKey);

        joinResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var result = await joinResponse.Content.ReadFromJsonAsync<JoinResponse>(JsonOptions);
        result.ShouldNotBeNull();
        result.Success.ShouldBeTrue();
        result.Role.ShouldBe("Client");
        result.PlayerId.ShouldBe(playerId);
        result.HostId.ShouldBe(hostId);
        string.IsNullOrWhiteSpace(result.SessionToken).ShouldBeFalse();
        result.Error.ShouldBeNull();
    }

    [Fact]
    public async Task JoinRoom_MissingRoom_ReturnsNotFound()
    {
        await using var factory = new HubApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await JoinRoomAsync(client, "NOEXIST", "Grace", Guid.NewGuid(), HubApplicationFactory.ApiKey);

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

        using var createResponse = await CreateRoomAsync(client, "Ada", Guid.NewGuid(), HubApplicationFactory.ApiKey);
        var createResult = await createResponse.Content.ReadFromJsonAsync<CreateRoomResponse>(JsonOptions);
        var roomCode = createResult!.RoomCode!;

        using var joinResponse = await JoinRoomAsync(client, roomCode, "Grace", Guid.NewGuid(), HubApplicationFactory.ApiKey);

        joinResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var result = await joinResponse.Content.ReadFromJsonAsync<JoinResponse>(JsonOptions);
        result.ShouldNotBeNull();
        result.Success.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
        result.Error!.Code.ShouldBe(HubErrorCode.HostNotReady);
    }

    [Fact]
    public async Task JoinRoom_DuplicateDisplayNames_Allowed()
    {
        await using var factory = new HubApplicationFactory();
        using var client = factory.CreateClient();
        var hostId = Guid.NewGuid();

        using var createResponse = await CreateRoomAsync(client, "Ada", hostId, HubApplicationFactory.ApiKey);
        var createResult = await createResponse.Content.ReadFromJsonAsync<CreateRoomResponse>(JsonOptions);
        var roomCode = createResult!.RoomCode!;

        await MarkReadyAsync(client, roomCode, hostId, HubApplicationFactory.ApiKey);

        using var join1 = await JoinRoomAsync(client, roomCode, "Grace", Guid.NewGuid(), HubApplicationFactory.ApiKey);
        using var join2 = await JoinRoomAsync(client, roomCode, "Grace", Guid.NewGuid(), HubApplicationFactory.ApiKey);

        join1.StatusCode.ShouldBe(HttpStatusCode.OK);
        join2.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task JoinRoom_WithInvalidPlayerName_ReturnsValidationError(string? playerName)
    {
        await using var factory = new HubApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await JoinRoomAsync(client, "ABC234", playerName!, Guid.NewGuid(), HubApplicationFactory.ApiKey);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task JoinRoom_WithEmptyPlayerId_ReturnsValidationError()
    {
        await using var factory = new HubApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await JoinRoomAsync(client, "ABC234", "Grace", Guid.Empty, HubApplicationFactory.ApiKey);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-the-configured-key")]
    public async Task JoinRoom_WithMissingOrInvalidApiKey_IsRejectedWithoutLeakingConfiguredKey(string? apiKey)
    {
        await using var factory = new HubApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await JoinRoomAsync(client, "ABC234", "Grace", Guid.NewGuid(), apiKey);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldNotContain(HubApplicationFactory.ApiKey);
    }

    [Fact]
    public async Task JoinRoom_ExceedsRateLimit_Returns429()
    {
        await using var factory = new HubApplicationFactory(joinRateLimitPerMinute: 2);
        using var client = factory.CreateClient();

        var hostId = Guid.NewGuid();
        using var createResponse = await CreateRoomAsync(client, "Ada", hostId, HubApplicationFactory.ApiKey);
        var createResult = await createResponse.Content.ReadFromJsonAsync<CreateRoomResponse>(JsonOptions);
        var roomCode = createResult!.RoomCode!;
        await MarkReadyAsync(client, roomCode, hostId, HubApplicationFactory.ApiKey);

        using var r1 = await JoinRoomAsync(client, roomCode, "Grace", Guid.NewGuid(), HubApplicationFactory.ApiKey);
        using var r2 = await JoinRoomAsync(client, roomCode, "Grace", Guid.NewGuid(), HubApplicationFactory.ApiKey);
        using var r3 = await JoinRoomAsync(client, roomCode, "Grace", Guid.NewGuid(), HubApplicationFactory.ApiKey);

        r3.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task MarkRoomReady_WithHostId_ReturnsOk()
    {
        await using var factory = new HubApplicationFactory();
        using var client = factory.CreateClient();
        var hostId = Guid.NewGuid();

        using var createResponse = await CreateRoomAsync(client, "Ada", hostId, HubApplicationFactory.ApiKey);
        var createResult = await createResponse.Content.ReadFromJsonAsync<CreateRoomResponse>(JsonOptions);
        var roomCode = createResult!.RoomCode!;

        using var response = await MarkReadyAsync(client, roomCode, hostId, HubApplicationFactory.ApiKey);

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

        using var createResponse = await CreateRoomAsync(client, "Ada", Guid.NewGuid(), HubApplicationFactory.ApiKey);
        var createResult = await createResponse.Content.ReadFromJsonAsync<CreateRoomResponse>(JsonOptions);
        var roomCode = createResult!.RoomCode!;

        using var response = await MarkReadyAsync(client, roomCode, Guid.NewGuid(), HubApplicationFactory.ApiKey);

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

        using var response = await MarkReadyAsync(client, "NOEXIST", Guid.NewGuid(), HubApplicationFactory.ApiKey);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var result = await response.Content.ReadFromJsonAsync<ReadyResponse>(JsonOptions);
        result.ShouldNotBeNull();
        result.Success.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
        result.Error!.Code.ShouldBe(HubErrorCode.RoomNotFound);
    }

    private static async Task<HttpResponseMessage> CreateRoomAsync(
        HttpClient client,
        string playerName,
        Guid playerId,
        string? apiKey)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/rooms");
        request.Content = JsonContent.Create(new CreateRoomRequest(playerName, playerId));

        if (apiKey is not null)
        {
            request.Headers.Add(ApiKeyAuthenticationDefaults.HeaderName, apiKey);
        }

        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> JoinRoomAsync(
        HttpClient client,
        string roomCode,
        string playerName,
        Guid playerId,
        string? apiKey)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/rooms/{roomCode}/join");
        request.Content = JsonContent.Create(new JoinRequest(playerName, playerId));

        if (apiKey is not null)
        {
            request.Headers.Add(ApiKeyAuthenticationDefaults.HeaderName, apiKey);
        }

        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> MarkReadyAsync(
        HttpClient client,
        string roomCode,
        Guid playerId,
        string? apiKey)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/rooms/{roomCode}/ready");
        request.Content = JsonContent.Create(new ReadyRequest(playerId));

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
