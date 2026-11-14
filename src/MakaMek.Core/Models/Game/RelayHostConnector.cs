using System.Reactive.Subjects;
using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MakaMek.Core.Services.Transport.Relay;
using Sanet.Transport;
using Sanet.Transport.SignalR.Client.Factories;
using Sanet.Transport.SignalR.Client.Relay;

namespace Sanet.MakaMek.Core.Models.Game;

/// <summary>
/// Owns the relay hosting bootstrap of <see cref="GameManager"/>: the hub configuration read, the
/// room creation, the relay ticket, the publisher connection and the room lock. It keeps the
/// session values (room code, session token, options, last error) so the manager only orchestrates
/// the lobby/server lifecycle and forwards the resulting state to its callers.
/// </summary>
internal sealed class RelayHostConnector(
    ICommandPublisher commandPublisher,
    IRelayRoomClient? relayRoomClient,
    IPublisherFactory? relayPublisherFactory,
    IRelayHubConfigurationProvider? relayHubConfigurationProvider,
    IOnlineStatusForwarder? forwarder,
    ILogger logger)
{
    private readonly BehaviorSubject<ConnectionStatus> _fallbackStatus =
        new(Sanet.MakaMek.Core.Services.Transport.ConnectionStatus.NotConnected);
    private ITransportPublisher? _publisher;
    private string? _sessionToken;
    private RelayClientOptions? _relayOptions;

    /// <summary>
    /// Gets the room code of the hosted room, or null when no online lobby is running.
    /// </summary>
    public string? RoomCode { get; private set; }

    /// <summary>
    /// Gets the last error reported by the hosting flow, if any.
    /// </summary>
    public RelayClientError? Error { get; private set; }

    /// <summary>
    /// Gets a value indicating whether a relay publisher is currently installed.
    /// </summary>
    public bool IsActive => _publisher != null;

    /// <summary>
    /// Gets the connection status of the online hosting session. Remains
    /// <see cref="ConnectionStatus.NotConnected"/> while no online room is being hosted.
    /// </summary>
    public IObservable<ConnectionStatus> ConnectionStatus =>
        forwarder?.OnlineConnectionStatus ?? _fallbackStatus;

    public void ClearError() => Error = null;

    /// <summary>
    /// Forgets the room of a previous session so a new lobby does not report a stale room code.
    /// </summary>
    public void ClearRoomState()
    {
        RoomCode = null;
        _sessionToken = null;
        _relayOptions = null;
    }

    /// <summary>
    /// Records the failure to lock the room of a previous hosting session.
    /// </summary>
    public void FailToLockActiveRoom() =>
        Error = new RelayClientError(
            RelayClientErrorCode.Unknown,
            "Failed to lock the currently active relay room.");

    /// <summary>
    /// Stops forwarding online status and resets the forwarded stream.
    /// </summary>
    public void ResetForwarding() => forwarder?.Reset();

    /// <summary>
    /// Locks the online relay room, if one is currently active. Returns true if the lock
    /// succeeded or no room was active; false if the lock failed or was cancelled. When false,
    /// state is not cleared allowing the caller to retry.
    /// </summary>
    public async Task<bool> LockAsync(CancellationToken cancellationToken = default)
    {
        // Only attempt to lock when an online room is actually active and we have
        // everything required to authenticate the lock call with the relay.
        if (_publisher == null
            || RoomCode == null
            || _sessionToken == null
            || relayRoomClient == null)
            return true;

        try
        {
            var lockResult = await relayRoomClient.Lock(RoomCode, _sessionToken, cancellationToken, _relayOptions);

            // Only clear the state when the lock operation succeeded, so a failed
            // attempt can be retried and the room is not considered locked prematurely.
            if (!lockResult.Success)
            {
                logger.LogWarning("Lock relay room {RoomCode} failed: {ErrorCode} {ErrorMessage}",
                    RoomCode, lockResult.Error?.Code, lockResult.Error?.Message);
                return false;
            }

            _sessionToken = null;
            RoomCode = null;
            _relayOptions = null;

            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Lock relay room {RoomCode} was cancelled", RoomCode);
            return false;
        }
        catch (Exception ex)
        {
            // Do not clear state; allow caller to retry
            logger.LogWarning(ex, "Failed to lock relay room {RoomCode}", RoomCode);
            return false;
        }
    }

    /// <summary>
    /// Reads the hub configuration pinned for the whole room lifecycle. Create, Ready, Lock and
    /// the publisher all target the hub that was selected when hosting began. Returns null and
    /// records an error when relay hosting is not configured on this platform.
    /// </summary>
    public async Task<RelayClientOptions?> GetOptionsForHostingAsync()
    {
        // Wait for persisted hub configuration before reading the active values below.
        var relayOptions = relayHubConfigurationProvider is null
            ? null
            : await relayHubConfigurationProvider.GetActiveOptions();

        // Relay hosting requires the room client, the publisher factory, and an active hub configuration
        if (relayRoomClient is null
            || relayPublisherFactory is null
            || relayOptions is null
            || string.IsNullOrWhiteSpace(relayOptions.BaseUrl))
        {
            Error = new RelayClientError(
                RelayClientErrorCode.ConfigurationError,
                "Relay hosting is not configured on this platform.");
            return null;
        }

        return relayOptions;
    }

    /// <summary>
    /// Creates the relay room and connects the host publisher to it. The room is disposed when
    /// the relay does not report the identity required to share it with joiners.
    /// </summary>
    /// <returns>True when the room was created, the publisher connected and the room marked ready.</returns>
    public async Task<bool> HostAsync(
        Guid gameId,
        RelayClientOptions relayOptions,
        CancellationToken cancellationToken)
    {
        var createResult = await relayRoomClient!.Create(
            RelayGameInfoFactory.Create(gameId),
            cancellationToken,
            relayOptions);
        if (!createResult.Success
            || createResult.RoomCode is null
            || createResult.SessionToken is null
            || createResult.GameInfo is null)
        {
            Error = createResult.Error
                ?? new RelayClientError(
                    RelayClientErrorCode.Unknown,
                    "The relay did not return the values required to host.");
            logger.LogWarning(
                "Failed to create relay room for game {GameId}: {ErrorCode} {ErrorMessage}",
                gameId,
                Error?.Code,
                Error?.Message);

            // The server game was created by the caller; it disposes it after a false result.
            await CleanupAsync();
            return false;
        }

        if (!await ConnectPublisherAsync(
                createResult.RoomCode,
                createResult.SessionToken,
                relayOptions,
                cancellationToken))
            return false;

        RoomCode = createResult.RoomCode;
        _sessionToken = createResult.SessionToken;
        _relayOptions = relayOptions;
        logger.LogInformation(
            "Hosted relay room {RoomCode} for game {GameId} {GameVersion}; relay publisher connected",
            createResult.RoomCode,
            createResult.GameInfo.HostId,
            createResult.GameInfo.Version);
        return true;
    }

    /// <summary>
    /// Connects the host publisher to the created room and confirms the room as ready, cleaning
    /// up the partial session on failure or cancellation so nothing is left orphaned. Returns
    /// false when the session was cleaned up instead of completed.
    /// </summary>
    private async Task<bool> ConnectPublisherAsync(
        string roomCode,
        string sessionToken,
        RelayClientOptions relayOptions,
        CancellationToken cancellationToken)
    {
        try
        {
            var ticket = await GetRelayTicketAsync(roomCode, sessionToken, relayOptions, cancellationToken);
            if (ticket is null)
            {
                await LockRoomAndCleanupAsync(roomCode, sessionToken, relayOptions);
                return false;
            }

            var publisher = await relayPublisherFactory!.Create(
                relayOptions.CreatePublisherOptions(roomCode, ticket),
                cancellationToken);

            commandPublisher.Adapter.AddPublisher(publisher);
            _publisher = publisher;
            forwarder?.Start(commandPublisher.Adapter);

            if (await MarkRoomReadyAsync(roomCode, sessionToken, relayOptions, cancellationToken))
                return true;

            await LockRoomAndCleanupAsync(roomCode, sessionToken, relayOptions);
            return false;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await LockRoomAndCleanupAsync(roomCode, sessionToken, relayOptions);
            throw;
        }
        catch (Exception)
        {
            Error = new RelayClientError(
                RelayClientErrorCode.Unknown,
                "Failed to connect the host to the relay.");
            await LockRoomAndCleanupAsync(roomCode, sessionToken, relayOptions);
            return false;
        }
    }

    private async Task<string?> GetRelayTicketAsync(
        string roomCode,
        string sessionToken,
        RelayClientOptions relayOptions,
        CancellationToken cancellationToken)
    {
        var ticketResult = await relayRoomClient!.GetRelayTicket(
            roomCode,
            sessionToken,
            cancellationToken,
            relayOptions);
        if (ticketResult.Success && !string.IsNullOrWhiteSpace(ticketResult.Ticket)) return ticketResult.Ticket;

        Error = ticketResult.Error
            ?? new RelayClientError(
                RelayClientErrorCode.Unknown,
                "The relay did not issue a relay ticket for the host session.");
        return null;
    }

    private async Task<bool> MarkRoomReadyAsync(
        string roomCode,
        string sessionToken,
        RelayClientOptions relayOptions,
        CancellationToken cancellationToken)
    {
        var readyResult = await relayRoomClient!.Ready(
            roomCode,
            sessionToken,
            cancellationToken,
            relayOptions);
        if (readyResult.Success) return true;

        Error = readyResult.Error
            ?? new RelayClientError(
                RelayClientErrorCode.Unknown,
                "The relay did not confirm the room as ready.");
        return false;
    }

    private async Task LockRoomAndCleanupAsync(
        string? roomCode,
        string? sessionToken,
        RelayClientOptions options)
    {
        // Best-effort lock of the relay room before local cleanup
        if (roomCode != null && sessionToken != null && relayRoomClient != null)
        {
            try
            {
                await relayRoomClient.Lock(roomCode, sessionToken, options: options);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to lock relay room {RoomCode} during failure cleanup", roomCode);
            }
        }

        await CleanupAsync();
    }

    /// <summary>
    /// Stops forwarding online status, removes and disposes the relay publisher and clears the
    /// session values.
    /// </summary>
    public async Task CleanupAsync()
    {
        // Stop forwarding online status before tearing down the relay publisher
        ResetForwarding();
        await RemovePublisherAsync();
    }

    /// <summary>
    /// Removes and disposes the relay publisher and clears the session values.
    /// </summary>
    public async Task RemovePublisherAsync()
    {
        await _publisher.RemoveAndDisposeAsync(commandPublisher.Adapter, logger);
        _publisher = null;
        _sessionToken = null;
        _relayOptions = null;
        RoomCode = null;
    }
}
