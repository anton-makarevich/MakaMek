using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Sanet.MakaMek.Hub.Contracts;
using Sanet.MakaMek.Hub.Security;
using Shouldly;

namespace Sanet.MakaMek.Hub.Tests.Rooms;

public class CreateRoomsEndpointTests
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    [Fact]
    public async Task CreateRoom_WithValidApiKey_CreatesHostRoomAndSession()
    {
        await using var factory = new HubApplicationFactory();
        using var client = factory.CreateClient();
        var playerId = Guid.NewGuid();

        using var response = await CreateRoomAsync(client, "Ada", playerId, HubApplicationFactory.ApiKey);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var result = await response.Content.ReadFromJsonAsync<CreateRoomResponse>(JsonOptions);

        result.ShouldNotBeNull();
        result.Success.ShouldBeTrue();
        result.HostId.ShouldBe(playerId);
        result.Error.ShouldBeNull();
        Regex.IsMatch(result.RoomCode!, "^[ABCDEFGHJKMNPQRSTUVWXYZ23456789]{6}$").ShouldBeTrue();
        string.IsNullOrWhiteSpace(result.SessionToken).ShouldBeFalse();
        result.ExpiresAt.ShouldNotBeNull();
        (result.ExpiresAt!.Value - DateTimeOffset.UtcNow).TotalMinutes.ShouldBeInRange(119, 121);
    }

    [Fact]
    public async Task CreateRoom_AtConfiguredCapacity_ReturnsHubAtCapacityAndActiveRoomCount()
    {
        await using var factory = new HubApplicationFactory(maxConcurrentRooms: 1);
        using var client = factory.CreateClient();

        using var firstResponse = await CreateRoomAsync(client, "Ada", Guid.NewGuid(), HubApplicationFactory.ApiKey);
        using var secondResponse = await CreateRoomAsync(client, "Grace", Guid.NewGuid(), HubApplicationFactory.ApiKey);

        firstResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        secondResponse.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);

        var result = await secondResponse.Content.ReadFromJsonAsync<CreateRoomResponse>(JsonOptions);

        result.ShouldNotBeNull();
        result.Success.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
        result.Error!.Code.ShouldBe(HubErrorCode.HubAtCapacity);
        result.Error.ActiveRoomCount.ShouldBe(1);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-the-configured-key")]
    public async Task CreateRoom_WithMissingOrInvalidApiKey_IsRejectedWithoutLeakingConfiguredKey(string? apiKey)
    {
        await using var factory = new HubApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await CreateRoomAsync(client, "Ada", Guid.NewGuid(), apiKey);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldNotContain(HubApplicationFactory.ApiKey);
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

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
