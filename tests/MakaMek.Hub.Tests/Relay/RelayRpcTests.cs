using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Sanet.MakaMek.Hub.Contracts;
using Sanet.MakaMek.Hub.Relay;
using Sanet.MakaMek.Hub.Security;
using Shouldly;

namespace Sanet.MakaMek.Hub.Tests.Relay;

public class RelayRpcTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task Relay_FansOutToOtherRoomMembers_Only()
    {
        await using var factory = new HubApplicationFactory();
        using var client = factory.CreateClient();

        var roomA = await CreateReadyHostAsync(client);
        var joinerA = await JoinRoomAsync(client, roomA.RoomCode, "Grace", Guid.NewGuid());
        var roomB = await CreateReadyHostAsync(client);

        await using var hostA = factory.CreateRelayHubConnection(
            HubApplicationFactory.ApiKey,
            roomA.SessionToken);
        await using var clientA = factory.CreateRelayHubConnection(
            HubApplicationFactory.ApiKey,
            joinerA.SessionToken!);
        await using var hostB = factory.CreateRelayHubConnection(
            HubApplicationFactory.ApiKey,
            roomB.SessionToken);

        var hostAReceived = new TaskCompletionSource<RelayEnvelope>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var clientAReceived = new TaskCompletionSource<RelayEnvelope>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var hostBReceived = new TaskCompletionSource<RelayEnvelope>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        hostA.On<RelayEnvelope>(nameof(IRelayHub.OnReceive), envelope => hostAReceived.TrySetResult(envelope));
        clientA.On<RelayEnvelope>(nameof(IRelayHub.OnReceive), envelope => clientAReceived.TrySetResult(envelope));
        hostB.On<RelayEnvelope>(nameof(IRelayHub.OnReceive), envelope => hostBReceived.TrySetResult(envelope));

        await hostA.StartAsync();
        await clientA.StartAsync();
        await hostB.StartAsync();

        var payload = """{"kind":"ping"}""";
        await hostA.InvokeAsync(
            nameof(RelayHub.Relay),
            roomA.RoomCode,
            new RelayEnvelope("client-supplied", payload, "1.0.0", 1, DateTime.UtcNow));

        var delivered = await clientAReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        delivered.Payload.ShouldBe(payload);

        var senderEcho = await Task.WhenAny(hostAReceived.Task, Task.Delay(500));
        senderEcho.ShouldNotBe(hostAReceived.Task);

        var otherRoom = await Task.WhenAny(hostBReceived.Task, Task.Delay(500));
        otherRoom.ShouldNotBe(hostBReceived.Task);
    }

    [Fact]
    public async Task Relay_OverwritesSenderId_WithConnectionId()
    {
        await using var factory = new HubApplicationFactory();
        using var client = factory.CreateClient();

        var host = await CreateReadyHostAsync(client);
        var joiner = await JoinRoomAsync(client, host.RoomCode, "Grace", Guid.NewGuid());

        await using var hostConnection = factory.CreateRelayHubConnection(
            HubApplicationFactory.ApiKey,
            host.SessionToken);
        await using var clientConnection = factory.CreateRelayHubConnection(
            HubApplicationFactory.ApiKey,
            joiner.SessionToken!);

        var received = new TaskCompletionSource<RelayEnvelope>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        clientConnection.On<RelayEnvelope>(
            nameof(IRelayHub.OnReceive),
            envelope => received.TrySetResult(envelope));

        await hostConnection.StartAsync();
        await clientConnection.StartAsync();

        await hostConnection.InvokeAsync(
            nameof(RelayHub.Relay),
            host.RoomCode,
            new RelayEnvelope("bogus-sender-id", "payload", "1.0.0", 7, DateTime.UtcNow));

        var envelope = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        envelope.SenderId.ShouldBe(hostConnection.ConnectionId);
        envelope.SenderId.ShouldNotBe("bogus-sender-id");
        envelope.Payload.ShouldBe("payload");
        envelope.SchemaVersion.ShouldBe("1.0.0");
        envelope.SequenceNumber.ShouldBe(7);
    }

    [Fact]
    public async Task Relay_WithMismatchedRoomCode_Throws_AndDoesNotFanOut()
    {
        await using var factory = new HubApplicationFactory();
        using var client = factory.CreateClient();

        var roomA = await CreateReadyHostAsync(client);
        var joinerA = await JoinRoomAsync(client, roomA.RoomCode, "Grace", Guid.NewGuid());
        var roomB = await CreateReadyHostAsync(client);

        await using var hostA = factory.CreateRelayHubConnection(
            HubApplicationFactory.ApiKey,
            roomA.SessionToken);
        await using var clientA = factory.CreateRelayHubConnection(
            HubApplicationFactory.ApiKey,
            joinerA.SessionToken!);

        var clientAReceived = new TaskCompletionSource<RelayEnvelope>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        clientA.On<RelayEnvelope>(
            nameof(IRelayHub.OnReceive),
            envelope => clientAReceived.TrySetResult(envelope));

        await hostA.StartAsync();
        await clientA.StartAsync();

        var exception = await Should.ThrowAsync<HubException>(async () =>
            await hostA.InvokeAsync(
                nameof(RelayHub.Relay),
                roomB.RoomCode,
                new RelayEnvelope("x", "should-not-deliver", "1.0.0", 1, DateTime.UtcNow)));

        exception.Message.ShouldNotContain(HubApplicationFactory.ApiKey);
        exception.Message.ShouldNotContain(roomA.SessionToken);

        var delivered = await Task.WhenAny(clientAReceived.Task, Task.Delay(500));
        delivered.ShouldNotBe(clientAReceived.Task);
    }

    [Fact]
    public async Task Relay_WithOversizedPayload_ReturnsMessageTooLarge_AndDoesNotFanOut()
    {
        await using var factory = new HubApplicationFactory();
        using var client = factory.CreateClient();

        var host = await CreateReadyHostAsync(client);
        var joiner = await JoinRoomAsync(client, host.RoomCode, "Grace", Guid.NewGuid());

        await using var hostConnection = factory.CreateRelayHubConnection(
            HubApplicationFactory.ApiKey,
            host.SessionToken);
        await using var clientConnection = factory.CreateRelayHubConnection(
            HubApplicationFactory.ApiKey,
            joiner.SessionToken!);

        var received = new TaskCompletionSource<RelayEnvelope>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        clientConnection.On<RelayEnvelope>(
            nameof(IRelayHub.OnReceive),
            envelope => received.TrySetResult(envelope));

        await hostConnection.StartAsync();
        await clientConnection.StartAsync();

        var oversizedPayload = new string('x', (256 * 1024) + 1);
        var exception = await Should.ThrowAsync<HubException>(async () =>
            await hostConnection.InvokeAsync(
                nameof(RelayHub.Relay),
                host.RoomCode,
                new RelayEnvelope("x", oversizedPayload, "1.0.0", 1, DateTime.UtcNow)));

        exception.Message.ShouldContain(nameof(HubErrorCode.MessageTooLarge));

        var delivered = await Task.WhenAny(received.Task, Task.Delay(500));
        delivered.ShouldNotBe(received.Task);
    }

    [Fact]
    public async Task Relay_ExceedsPerConnectionRateLimit_ReturnsRateLimited_AndDoesNotFanOut()
    {
        await using var factory = new HubApplicationFactory(relayRateLimitPerMinute: 2);
        using var client = factory.CreateClient();

        var host = await CreateReadyHostAsync(client);
        var joiner = await JoinRoomAsync(client, host.RoomCode, "Grace", Guid.NewGuid());

        await using var hostConnection = factory.CreateRelayHubConnection(
            HubApplicationFactory.ApiKey,
            host.SessionToken);
        await using var clientConnection = factory.CreateRelayHubConnection(
            HubApplicationFactory.ApiKey,
            joiner.SessionToken!);

        var receiveCount = 0;
        var thirdAttemptReceived = new TaskCompletionSource<RelayEnvelope>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        clientConnection.On<RelayEnvelope>(nameof(IRelayHub.OnReceive), envelope =>
        {
            var count = Interlocked.Increment(ref receiveCount);
            if (count > 2)
            {
                thirdAttemptReceived.TrySetResult(envelope);
            }
        });

        await hostConnection.StartAsync();
        await clientConnection.StartAsync();

        await hostConnection.InvokeAsync(
            nameof(RelayHub.Relay),
            host.RoomCode,
            new RelayEnvelope("x", "one", "1.0.0", 1, DateTime.UtcNow));
        await hostConnection.InvokeAsync(
            nameof(RelayHub.Relay),
            host.RoomCode,
            new RelayEnvelope("x", "two", "1.0.0", 2, DateTime.UtcNow));

        var exception = await Should.ThrowAsync<HubException>(async () =>
            await hostConnection.InvokeAsync(
                nameof(RelayHub.Relay),
                host.RoomCode,
                new RelayEnvelope("x", "three", "1.0.0", 3, DateTime.UtcNow)));

        exception.Message.ShouldContain(nameof(HubErrorCode.RateLimited));

        var delivered = await Task.WhenAny(thirdAttemptReceived.Task, Task.Delay(500));
        delivered.ShouldNotBe(thirdAttemptReceived.Task);
        receiveCount.ShouldBe(2);
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

    private sealed record ReadyHost(string RoomCode, string SessionToken);
}
