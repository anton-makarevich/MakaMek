using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Sanet.MakaMek.Hub.Contracts;
using Sanet.MakaMek.Hub.Relay;
using Sanet.MakaMek.Hub.Security;
using Shouldly;

namespace Sanet.MakaMek.Hub.Tests.Relay;

public class RelayLifecycleTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task ClientLifecycle_NotifiesHostWithActualConnectionId()
    {
        await using var factory = new HubApplicationFactory();
        using var httpClient = factory.CreateClient();
        var room = await CreateReadyRoomAsync(httpClient);
        var clientSession = await JoinRoomAsync(httpClient, room.RoomCode, sessionToken: null);
        await using var host = factory.CreateRelayHubConnection(HubApplicationFactory.ApiKey, room.HostToken);
        await using var client = factory.CreateRelayHubConnection(HubApplicationFactory.ApiKey, clientSession);
        var connected = NewCompletionSource<string>();
        var disconnected = NewCompletionSource<string>();
        host.On<string>(nameof(IRelayHub.OnPeerConnected), id => connected.TrySetResult(id));
        host.On<string>(nameof(IRelayHub.OnPeerDisconnected), id => disconnected.TrySetResult(id));

        await host.StartAsync();
        await client.StartAsync();

        (await connected.Task.WaitAsync(TimeSpan.FromSeconds(5))).ShouldBe(client.ConnectionId);
        var clientConnectionId = client.ConnectionId;
        await client.StopAsync();
        (await disconnected.Task.WaitAsync(TimeSpan.FromSeconds(5))).ShouldBe(clientConnectionId);
    }

    [Fact]
    public async Task SecondClientConnection_ReplacesFirstAndReportsDisconnectThenConnect()
    {
        await using var factory = new HubApplicationFactory();
        using var httpClient = factory.CreateClient();
        var room = await CreateReadyRoomAsync(httpClient);
        var clientSession = await JoinRoomAsync(httpClient, room.RoomCode, sessionToken: null);
        await using var host = factory.CreateRelayHubConnection(HubApplicationFactory.ApiKey, room.HostToken);
        await using var first = factory.CreateRelayHubConnection(HubApplicationFactory.ApiKey, clientSession);
        await using var second = factory.CreateRelayHubConnection(HubApplicationFactory.ApiKey, clientSession);
        var events = new ConcurrentQueue<(string Kind, string Id)>();
        var transition = NewCompletionSource<bool>();
        var firstReceived = NewCompletionSource<RelayEnvelope>();
        var secondReceived = NewCompletionSource<RelayEnvelope>();
        first.On<RelayEnvelope>(nameof(IRelayHub.OnReceive), envelope => firstReceived.TrySetResult(envelope));
        second.On<RelayEnvelope>(nameof(IRelayHub.OnReceive), envelope => secondReceived.TrySetResult(envelope));
        host.On<string>(nameof(IRelayHub.OnPeerConnected), id =>
        {
            events.Enqueue(("connected", id));
            if (events.Count == 3) transition.TrySetResult(true);
        });
        host.On<string>(nameof(IRelayHub.OnPeerDisconnected), id =>
        {
            events.Enqueue(("disconnected", id));
            if (events.Count == 3) transition.TrySetResult(true);
        });

        await host.StartAsync();
        await first.StartAsync();
        await WaitUntilAsync(() => events.Count == 1);
        await second.StartAsync();
        await transition.Task.WaitAsync(TimeSpan.FromSeconds(5));

        events.ToArray().ShouldBe([
            ("connected", first.ConnectionId!),
            ("disconnected", first.ConnectionId!),
            ("connected", second.ConnectionId!)
        ]);

        await host.InvokeAsync(nameof(RelayHub.Relay), room.RoomCode,
            new RelayEnvelope("ignored", "replacement-only", "1.0.0", 1, DateTime.UtcNow));
        (await secondReceived.Task.WaitAsync(TimeSpan.FromSeconds(5))).Payload.ShouldBe("replacement-only");
        (await Task.WhenAny(firstReceived.Task, Task.Delay(300))).ShouldNotBe(firstReceived.Task);
    }

    [Fact]
    public async Task HostLoss_NotifiesRemainingPeerWithHostDisconnected()
    {
        await using var factory = new HubApplicationFactory();
        using var httpClient = factory.CreateClient();
        var room = await CreateReadyRoomAsync(httpClient);
        var clientSession = await JoinRoomAsync(httpClient, room.RoomCode, sessionToken: null);
        await using var host = factory.CreateRelayHubConnection(HubApplicationFactory.ApiKey, room.HostToken);
        await using var client = factory.CreateRelayHubConnection(HubApplicationFactory.ApiKey, clientSession);
        var error = NewCompletionSource<HubError>();
        client.On<HubError>(nameof(IRelayHub.OnError), value => error.TrySetResult(value));
        await host.StartAsync();
        await WaitForPeerConnectedAsync(host, client);

        await host.StopAsync();

        (await error.Task.WaitAsync(TimeSpan.FromSeconds(5))).Code.ShouldBe(HubErrorCode.HostDisconnected);
    }

    [Fact]
    public async Task HostReconnectWithinGrace_CancelsDissolutionAndRelayResumes()
    {
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 7, 24, 12, 0, 0, TimeSpan.Zero));
        await using var factory = new HubApplicationFactory(timeProvider: clock);
        using var httpClient = factory.CreateClient();
        var room = await CreateReadyRoomAsync(httpClient);
        var clientSession = await JoinRoomAsync(httpClient, room.RoomCode, sessionToken: null);
        await using var host = factory.CreateRelayHubConnection(HubApplicationFactory.ApiKey, room.HostToken);
        await using var client = factory.CreateRelayHubConnection(HubApplicationFactory.ApiKey, clientSession);
        var hostDisconnected = NewCompletionSource<HubError>();
        client.On<HubError>(nameof(IRelayHub.OnError), error => hostDisconnected.TrySetResult(error));
        await host.StartAsync();
        await WaitForPeerConnectedAsync(host, client);
        await host.StopAsync();
        await hostDisconnected.Task.WaitAsync(TimeSpan.FromSeconds(5));
        clock.Advance(TimeSpan.FromSeconds(29));
        await using var reconnectedHost = factory.CreateRelayHubConnection(HubApplicationFactory.ApiKey, room.HostToken);
        var received = NewCompletionSource<RelayEnvelope>();
        reconnectedHost.On<RelayEnvelope>(nameof(IRelayHub.OnReceive), envelope => received.TrySetResult(envelope));

        await reconnectedHost.StartAsync();
        await WaitUntilAsync(async () =>
        {
            try
            {
                await client.InvokeAsync(nameof(RelayHub.Relay), room.RoomCode,
                    new RelayEnvelope("ignored", "resumed", "1.0.0", 1, DateTime.UtcNow));
            }
            catch (HubException exception) when (exception.Message.Contains(nameof(HubErrorCode.ConnectionSuperseded)))
            {
                return false;
            }
            return received.Task.IsCompleted;
        });

        (await received.Task.WaitAsync(TimeSpan.FromSeconds(5))).Payload.ShouldBe("resumed");
    }

    [Fact]
    public async Task HostReconnectAfterGrace_IsRejectedBecauseRoomWasDissolved()
    {
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 7, 24, 12, 0, 0, TimeSpan.Zero));
        await using var factory = new HubApplicationFactory(timeProvider: clock);
        using var httpClient = factory.CreateClient();
        var room = await CreateReadyRoomAsync(httpClient);
        var clientSession = await JoinRoomAsync(httpClient, room.RoomCode, sessionToken: null);
        await using var host = factory.CreateRelayHubConnection(HubApplicationFactory.ApiKey, room.HostToken);
        await using var client = factory.CreateRelayHubConnection(HubApplicationFactory.ApiKey, clientSession);
        var hostDisconnected = NewCompletionSource<HubError>();
        client.On<HubError>(nameof(IRelayHub.OnError), error => hostDisconnected.TrySetResult(error));
        await host.StartAsync();
        await WaitForPeerConnectedAsync(host, client);
        await host.StopAsync();
        await hostDisconnected.Task.WaitAsync(TimeSpan.FromSeconds(5));
        clock.Advance(TimeSpan.FromSeconds(30));
        await using var reconnect = factory.CreateRelayHubConnection(HubApplicationFactory.ApiKey, room.HostToken);

        await Should.ThrowAsync<Exception>(async () => await reconnect.StartAsync());
    }

    [Fact]
    public async Task HostReconnectAfterNonDefaultGrace_IsRejectedBecauseRoomWasDissolved()
    {
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 7, 24, 12, 0, 0, TimeSpan.Zero));
        await using var factory = new HubApplicationFactory(
            dissolutionGracePeriodSeconds: 60,
            timeProvider: clock);
        using var httpClient = factory.CreateClient();
        var room = await CreateReadyRoomAsync(httpClient);
        var clientSession = await JoinRoomAsync(httpClient, room.RoomCode, sessionToken: null);
        await using var host = factory.CreateRelayHubConnection(HubApplicationFactory.ApiKey, room.HostToken);
        await using var client = factory.CreateRelayHubConnection(HubApplicationFactory.ApiKey, clientSession);
        var hostDisconnected = NewCompletionSource<HubError>();
        client.On<HubError>(nameof(IRelayHub.OnError), error => hostDisconnected.TrySetResult(error));
        await host.StartAsync();
        await WaitForPeerConnectedAsync(host, client);
        await host.StopAsync();
        await hostDisconnected.Task.WaitAsync(TimeSpan.FromSeconds(5));
        // Advance beyond the non-default 60-second grace period.
        clock.Advance(TimeSpan.FromSeconds(61));
        await using var reconnect = factory.CreateRelayHubConnection(HubApplicationFactory.ApiKey, room.HostToken);

        await Should.ThrowAsync<Exception>(async () => await reconnect.StartAsync());
    }

    [Fact]
    public async Task HostReconnectBeforeNonDefaultGrace_SucceedsBeforeDissolution()
    {
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 7, 24, 12, 0, 0, TimeSpan.Zero));
        await using var factory = new HubApplicationFactory(
            dissolutionGracePeriodSeconds: 60,
            timeProvider: clock);
        using var httpClient = factory.CreateClient();
        var room = await CreateReadyRoomAsync(httpClient);
        var clientSession = await JoinRoomAsync(httpClient, room.RoomCode, sessionToken: null);
        await using var host = factory.CreateRelayHubConnection(HubApplicationFactory.ApiKey, room.HostToken);
        await using var client = factory.CreateRelayHubConnection(HubApplicationFactory.ApiKey, clientSession);
        var hostDisconnected = NewCompletionSource<HubError>();
        client.On<HubError>(nameof(IRelayHub.OnError), error => hostDisconnected.TrySetResult(error));
        await host.StartAsync();
        await WaitForPeerConnectedAsync(host, client);
        await host.StopAsync();
        await hostDisconnected.Task.WaitAsync(TimeSpan.FromSeconds(5));
        // Advance past the default 30-second grace but before the configured 60-second grace.
        clock.Advance(TimeSpan.FromSeconds(31));
        await using var reconnect = factory.CreateRelayHubConnection(HubApplicationFactory.ApiKey, room.HostToken);

        await reconnect.StartAsync();
    }

    private static async Task<ReadyRoom> CreateReadyRoomAsync(HttpClient client)
    {
        using var createRequest = new HttpRequestMessage(HttpMethod.Post, "/api/rooms");
        createRequest.Content = JsonContent.Create(new CreateRoomRequest(Guid.NewGuid()));
        createRequest.Headers.Add(ApiKeyAuthenticationDefaults.HeaderName, HubApplicationFactory.ApiKey);
        using var createResponse = await client.SendAsync(createRequest);
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<CreateRoomResponse>(JsonOptions);
        created.ShouldNotBeNull();

        using var readyRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/rooms/{created.RoomCode}/ready");
        readyRequest.Headers.Add(ApiKeyAuthenticationDefaults.HeaderName, HubApplicationFactory.ApiKey);
        readyRequest.Headers.Add("Session-Token", created.SessionToken);
        using var readyResponse = await client.SendAsync(readyRequest);
        readyResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        return new ReadyRoom(created.RoomCode!, created.SessionToken!);
    }

    private static async Task<string> JoinRoomAsync(HttpClient client, string roomCode, string? sessionToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/rooms/{roomCode}/join");
        if (sessionToken is not null)
        {
            request.Headers.Add("Session-Token", sessionToken);
        }
        request.Headers.Add(ApiKeyAuthenticationDefaults.HeaderName, HubApplicationFactory.ApiKey);
        using var response = await client.SendAsync(request);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var joined = await response.Content.ReadFromJsonAsync<JoinResponse>(JsonOptions);
        joined.ShouldNotBeNull();
        return joined.SessionToken!;
    }

    private static async Task WaitForPeerConnectedAsync(HubConnection host, HubConnection client)
    {
        var connected = NewCompletionSource<string>();
        host.On<string>(nameof(IRelayHub.OnPeerConnected), id => connected.TrySetResult(id));
        await client.StartAsync();
        await connected.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    private static TaskCompletionSource<T> NewCompletionSource<T>() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static async Task WaitUntilAsync(Func<bool> predicate)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!predicate() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }
        predicate().ShouldBeTrue();
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> predicate)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        var succeeded = false;
        while (DateTime.UtcNow < deadline)
        {
            succeeded = await predicate();
            if (succeeded)
            {
                return;
            }
            await Task.Delay(10);
        }
        succeeded.ShouldBeTrue();
    }

    private sealed record ReadyRoom(string RoomCode, string HostToken);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan offset) => _now += offset;
    }
}
