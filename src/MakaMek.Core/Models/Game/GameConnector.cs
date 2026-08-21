using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.Transport;
using Sanet.Transport.SignalR.Client.Factories;
using Sanet.Transport.SignalR.Client.Relay;

namespace Sanet.MakaMek.Core.Models.Game;

/// <summary>
/// Client-side counterpart of <see cref="GameManager"/>: owns the transport publishers used to
/// connect to a LAN server or join an online relay room and exposes connection state to the UI.
/// </summary>
public class GameConnector : IGameConnector
{
    private readonly ICommandPublisher _commandPublisher;
    private readonly ITransportFactory _transportFactory;
    private readonly IRelayRoomClient? _relayRoomClient;
    private readonly IPublisherFactory? _relayPublisherFactory;
    private readonly IRelayHubConfigurationProvider? _relayHubConfigurationProvider;
    private readonly ILogger<GameConnector> _logger;

    private ITransportPublisher? _relayPublisher;
    private ITransportPublisher? _lanPublisher;
    private string? _roomCode;
    private string? _sessionToken;
    private Guid? _deviceSessionId;
    private bool _isDisposed;

    public GameConnector(
        ICommandPublisher commandPublisher,
        ITransportFactory transportFactory,
        ILogger<GameConnector> logger,
        IRelayRoomClient? relayRoomClient = null,
        IPublisherFactory? relayPublisherFactory = null,
        IRelayHubConfigurationProvider? relayHubConfigurationProvider = null)
    {
        _commandPublisher = commandPublisher;
        _transportFactory = transportFactory;
        _logger = logger;
        _relayRoomClient = relayRoomClient;
        _relayPublisherFactory = relayPublisherFactory;
        _relayHubConfigurationProvider = relayHubConfigurationProvider;
    }

    public bool IsConnected { get; private set; }

    public Guid? ConnectedHostGameId { get; private set; }

    public RelayClientError? OnlineError { get; private set; }

    public async Task ConnectToLan(string serverAddress)
    {
        OnlineError = null;

        ITransportPublisher? publisher = null;
        try
        {
            publisher = await _transportFactory.CreateAndStartClientPublisher(serverAddress);

            var adapter = _commandPublisher.Adapter;
            // Remove only the previously owned LAN publisher; other flows' publishers
            // (e.g. the shared local RxTransportPublisher) must stay registered.
            await _lanPublisher.RemoveAndDisposeAsync(adapter, _logger);
            _lanPublisher = null;
            adapter.AddPublisher(publisher);
            _lanPublisher = publisher;

            ConnectedHostGameId = null;
            IsConnected = true;
            _logger.LogInformation("Connected to LAN server at {ServerAddress}", serverAddress);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to LAN server at {ServerAddress}", serverAddress);
            if (publisher is IAsyncDisposable asyncDisposable)
            {
                try
                {
                    await asyncDisposable.DisposeAsync();
                }
                catch
                {
                    // Swallow to avoid masking the original failure
                }
            }
            IsConnected = false;
        }
    }

    public async Task JoinOnline(
        string roomCode,
        string? sessionToken,
        CancellationToken cancellationToken = default)
    {
        OnlineError = null;

        // Wait for persisted hub configuration before reading the active values below
        var relayOptions = _relayHubConfigurationProvider is null
            ? null
            : await _relayHubConfigurationProvider.GetActiveOptions();

        // Online joining requires the relay room client, publisher factory, and an active hub configuration
        if (_relayRoomClient is null
            || _relayPublisherFactory is null
            || relayOptions is null
            || string.IsNullOrWhiteSpace(relayOptions.BaseUrl))
        {
            OnlineError = new RelayClientError(
                RelayClientErrorCode.ConfigurationError,
                "Relay joining is not configured on this platform.");
            return;
        }

        ITransportPublisher? publisher = null;
        string? successfulSessionToken = null;
        Guid? successfulDeviceSessionId = null;
        try
        {
            var joinResult = await _relayRoomClient.Join(roomCode, sessionToken, cancellationToken);
            if (!joinResult.Success || joinResult.SessionToken is null || joinResult.HostGameId is null)
            {
                OnlineError = joinResult.Error
                    ?? new RelayClientError(
                        RelayClientErrorCode.Unknown,
                        "The relay did not return the values required to join.");
                return;
            }
            
            successfulSessionToken = joinResult.SessionToken;
            successfulDeviceSessionId = joinResult.DeviceSessionId;

            var baseUrl = relayOptions.BaseUrl;
            var hubUrl = RelayHubDefaults.BuildHubUrl(baseUrl);

            var ticketResult = await _relayRoomClient.GetRelayTicket(
                roomCode,
                joinResult.SessionToken,
                cancellationToken,
                relayOptions);
            if (!ticketResult.Success || string.IsNullOrWhiteSpace(ticketResult.Ticket))
            {
                OnlineError = ticketResult.Error
                    ?? new RelayClientError(
                        RelayClientErrorCode.Unknown,
                        "The relay did not issue a relay ticket for the joining session.");
                if (joinResult.DeviceSessionId is { } joinedDeviceId)
                    await RemoveRelayMembership(roomCode, joinResult.SessionToken, joinedDeviceId);
                return;
            }

            publisher = await _relayPublisherFactory.Create(
                new RelayPublisherOptions
                {
                    HubUrl = hubUrl,
                    RoomCode = roomCode,
                    RelayTicket = ticketResult.Ticket
                },
                cancellationToken);

            // Throw if cancelled; the cancellation catch block below is the single
            // owner of publisher removal and RemoveRelayMembership cleanup
            cancellationToken.ThrowIfCancellationRequested();

            var adapter = _commandPublisher.Adapter;
            // Remove only the previously owned relay publisher; other flows' publishers
            // (e.g. the shared local RxTransportPublisher) must stay registered.
            await _relayPublisher.RemoveAndDisposeAsync(adapter, _logger);
            _relayPublisher = null;
            adapter.AddPublisher(publisher);
            adapter.RegisterDisconnectHandler(OnRelayHostDisconnected);

            _relayPublisher = publisher;
            _roomCode = roomCode;
            _sessionToken = joinResult.SessionToken;
            _deviceSessionId = joinResult.DeviceSessionId;

            ConnectedHostGameId = joinResult.HostGameId;
            IsConnected = true;
            _logger.LogInformation(
                "Joined relay room {RoomCode} connected to host game {HostGameId}",
                roomCode,
                joinResult.HostGameId.Value);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await publisher.RemoveAndDisposeAsync(_commandPublisher.Adapter, _logger);
            if (successfulSessionToken != null && successfulDeviceSessionId != null)
                await RemoveRelayMembership(roomCode, successfulSessionToken, successfulDeviceSessionId.Value);
            throw;
        }
        catch (Exception)
        {
            OnlineError = new RelayClientError(
                RelayClientErrorCode.NetworkError,
                "Failed to connect the client to the relay.");
            await publisher.RemoveAndDisposeAsync(_commandPublisher.Adapter, _logger);
            if (successfulSessionToken != null && successfulDeviceSessionId != null)
                await RemoveRelayMembership(roomCode, successfulSessionToken, successfulDeviceSessionId.Value);
        }
    }

    /// <summary>
    /// Invoked when the relay publisher reports that the host has disconnected from the room.
    /// Synthesizes a local <see cref="GameEndedCommand"/> delivered through the shared command
    /// pipeline so the client reacts the same way it would if the server had sent the command,
    /// without requiring further network traffic. The command is dispatched through the local
    /// receive path rather than published to the (now disconnected) relay publisher.
    /// </summary>
    /// <param name="publisher">The publisher that lost its connection to the host.</param>
    private void OnRelayHostDisconnected(ITransportPublisher publisher)
    {
        if (publisher != _relayPublisher) return;
        if (!IsConnected) return;

        IsConnected = false;

        var hostGameId = ConnectedHostGameId;
        ConnectedHostGameId = null;

        var command = new GameEndedCommand
        {
            GameOriginId = hostGameId ?? Guid.NewGuid(),
            Reason = GameEndReason.HostDisconnected,
            Timestamp = DateTime.UtcNow
        };
        _commandPublisher.Adapter.DispatchLocalCommand(command, publisher);
    }

    public Task Disconnect(CancellationToken cancellationToken = default) =>
        Teardown();

    private async Task RemoveRelayMembership(string roomCode, string sessionToken, Guid deviceSessionId)
    {
        if (_relayRoomClient == null) return;
        try
        {
            await _relayRoomClient.RemoveMember(roomCode, sessionToken, deviceSessionId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to remove device session from relay room {RoomCode}", roomCode);
        }
    }

    private async Task Teardown()
    {
        // Best-effort guest leave of the online room, if one is active
        if (_relayRoomClient != null && _roomCode != null && _sessionToken != null && _deviceSessionId != null)
        {
            await RemoveRelayMembership(_roomCode, _sessionToken, _deviceSessionId.Value);
        }

        // Remove and dispose only connector-owned publishers; other flows' publishers
        // (e.g. the shared local RxTransportPublisher) must stay registered on the adapter.
        await _relayPublisher.RemoveAndDisposeAsync(_commandPublisher.Adapter, _logger);
        _relayPublisher = null;
        _roomCode = null;
        _sessionToken = null;
        _deviceSessionId = null;

        await _lanPublisher.RemoveAndDisposeAsync(_commandPublisher.Adapter, _logger);
        _lanPublisher = null;

        ConnectedHostGameId = null;
        IsConnected = false;
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        
        IsConnected = false;
        ConnectedHostGameId = null;
        _relayPublisher = null;
        _lanPublisher = null;
        _roomCode = null;
        _sessionToken = null;
        _deviceSessionId = null;
        
        _isDisposed = true;
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        await Teardown();

        GC.SuppressFinalize(this);
    }
}
