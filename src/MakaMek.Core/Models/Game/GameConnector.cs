using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MakaMek.Core.Services.Transport.Relay;
using Sanet.Transport;
using Sanet.Transport.SignalR.Client.Publishers;

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
    private readonly IRelayPublisherFactory? _relayPublisherFactory;
    private readonly IOptions<RelayClientOptions>? _relayOptions;
    private readonly ILogger<GameConnector> _logger;

    private RelayClientPublisher? _relayPublisher;
    private string? _roomCode;
    private string? _sessionToken;
    private Guid? _playerId;
    private bool _isDisposed;

    public GameConnector(
        ICommandPublisher commandPublisher,
        ITransportFactory transportFactory,
        ILogger<GameConnector> logger,
        IRelayRoomClient? relayRoomClient = null,
        IRelayPublisherFactory? relayPublisherFactory = null,
        IOptions<RelayClientOptions>? relayOptions = null)
    {
        _commandPublisher = commandPublisher;
        _transportFactory = transportFactory;
        _logger = logger;
        _relayRoomClient = relayRoomClient;
        _relayPublisherFactory = relayPublisherFactory;
        _relayOptions = relayOptions;
    }

    public bool IsConnected { get; private set; }

    public RelayClientError? OnlineError { get; private set; }

    public async Task ConnectToLan(string serverAddress)
    {
        OnlineError = null;

        ITransportPublisher? publisher = null;
        try
        {
            publisher = await _transportFactory.CreateAndStartClientPublisher(serverAddress);

            var adapter = _commandPublisher.Adapter;
            await adapter.ClearPublishers();
            adapter.AddPublisher(publisher);

            IsConnected = true;
        }
        catch (Exception)
        {
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
        Guid playerId,
        string playerName,
        CancellationToken cancellationToken = default)
    {
        OnlineError = null;

        // Online joining requires the relay room client, publisher factory, and options
        if (_relayRoomClient is null || _relayPublisherFactory is null || _relayOptions is null)
        {
            OnlineError = new RelayClientError(
                RelayClientErrorCode.ConfigurationError,
                "Relay joining is not configured on this platform.");
            return;
        }

        RelayClientPublisher? publisher = null;
        string? successfulSessionToken = null;
        Guid? successfulPlayerId = null;
        try
        {
            var joinResult = await _relayRoomClient.JoinAsync(roomCode, playerId, playerName, cancellationToken);
            if (!joinResult.Success || joinResult.SessionToken is null || joinResult.HostId is null)
            {
                OnlineError = joinResult.Error
                    ?? new RelayClientError(
                        RelayClientErrorCode.Unknown,
                        "The relay did not return the values required to join.");
                return;
            }
            
            successfulSessionToken = joinResult.SessionToken;
            successfulPlayerId = playerId;

            var baseUrl = _relayOptions.Value.BaseUrl;
            var hubUrl = RelayHubDefaults.BuildHubUrl(baseUrl);

            publisher = await _relayPublisherFactory.CreateAsync(
                hubUrl,
                roomCode,
                joinResult.SessionToken,
                joinResult.HostId.Value,
                cancellationToken);

            // Throw if cancelled; the cancellation catch block below is the single
            // owner of RemoveAndDisposeOnlinePublisher and RemoveRelayMembership cleanup
            cancellationToken.ThrowIfCancellationRequested();

            var adapter = _commandPublisher.Adapter;
            await adapter.ClearPublishers();
            adapter.AddPublisher(publisher);
            adapter.RegisterDisconnectHandler(OnRelayHostDisconnected);

            _relayPublisher = publisher;
            _roomCode = roomCode;
            _sessionToken = joinResult.SessionToken;
            _playerId = playerId;

            IsConnected = true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await RemoveAndDisposeOnlinePublisher(publisher);
            if (successfulSessionToken != null && successfulPlayerId != null)
                await RemoveRelayMembership(roomCode, successfulSessionToken, successfulPlayerId.Value);
            throw;
        }
        catch (Exception)
        {
            OnlineError = new RelayClientError(
                RelayClientErrorCode.NetworkError,
                "Failed to connect the client to the relay.");
            await RemoveAndDisposeOnlinePublisher(publisher);
            if (successfulSessionToken != null && successfulPlayerId != null)
                await RemoveRelayMembership(roomCode, successfulSessionToken, successfulPlayerId.Value);
        }
    }

    /// <summary>
    /// Invoked when the relay publisher reports that the host has disconnected from the room.
    /// Synthesizes a local <see cref="GameEndedCommand"/> delivered through the shared command
    /// pipeline so the client reacts the same way it would if the server had sent the command,
    /// without requiring further network traffic.
    /// </summary>
    /// <param name="publisher">The publisher that lost its connection to the host.</param>
    private void OnRelayHostDisconnected(ITransportPublisher publisher)
    {
        if (publisher != _relayPublisher) return;
        if (!IsConnected) return;

        IsConnected = false;
        
        var command = new GameEndedCommand
        {
            GameOriginId = Guid.NewGuid(),
            Reason = GameEndReason.HostDisconnected,
            Timestamp = DateTime.UtcNow
        };
        _commandPublisher.PublishCommand(command);
    }

    private async Task RemoveAndDisposeOnlinePublisher(RelayClientPublisher? publisher)
    {
        // Remove and dispose the relay publisher if it was created
        if (publisher == null) return;
        try
        {
            _commandPublisher.Adapter.RemovePublisher(publisher);
        }
        catch (Exception ex)
        {
            // Swallow to avoid masking the original failure
            _logger.LogWarning(ex, "Failed to remove relay publisher during cleanup");
        }
        try
        {
            await publisher.DisposeAsync();
        }
        catch (Exception ex)
        {
            // Swallow to avoid masking the original failure
            _logger.LogWarning(ex, "Failed to dispose relay publisher during cleanup");
        }
    }

    public Task Disconnect(CancellationToken cancellationToken = default) =>
        Teardown();

    private async Task RemoveRelayMembership(string roomCode, string sessionToken, Guid playerId)
    {
        if (_relayRoomClient == null) return;
        try
        {
            await _relayRoomClient.RemoveMemberAsync(roomCode, sessionToken, playerId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to remove member from relay room {RoomCode}", roomCode);
        }
    }

    private async Task Teardown()
    {
        // Best-effort guest leave of the online room, if one is active
        if (_relayRoomClient != null && _roomCode != null && _sessionToken != null && _playerId != null)
        {
            await RemoveRelayMembership(_roomCode, _sessionToken, _playerId.Value);
        }

        // Remove and dispose the relay publisher if it was created
        await RemoveAndDisposeOnlinePublisher(_relayPublisher);
        _relayPublisher = null;
        _roomCode = null;
        _sessionToken = null;
        _playerId = null;

        try
        {
            await _commandPublisher.Adapter.ClearPublishers();
        }
        catch (Exception ex)
        {
            // Swallow to avoid masking the original failure
            _logger.LogWarning(ex, "Failed to clear publishers during disconnect");
        }

        IsConnected = false;
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        
        IsConnected = false;
        _relayPublisher = null;
        _roomCode = null;
        _sessionToken = null;
        _playerId = null;
        
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
