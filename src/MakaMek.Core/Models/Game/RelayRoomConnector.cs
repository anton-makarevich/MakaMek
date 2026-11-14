using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MakaMek.Core.Services.Transport.Relay;
using Sanet.Transport;
using Sanet.Transport.Relay.Contracts;
using Sanet.Transport.SignalR.Client.Factories;
using Sanet.Transport.SignalR.Client.Relay;

namespace Sanet.MakaMek.Core.Models.Game;

/// <summary>
/// The publisher and session values of a successful relay room join, or the error that prevented
/// it.
/// </summary>
internal sealed record RelayConnectionResult(
    ITransportPublisher? Publisher,
    RelayClientError? Error,
    string? SessionToken,
    Guid? DeviceSessionId,
    Guid HostGameId,
    string GameId,
    string? Version)
{
    public static RelayConnectionResult Failed(RelayClientError error) =>
        new(null, error, null, null, Guid.Empty, string.Empty, null);

    public static RelayConnectionResult Succeeded(
        ITransportPublisher publisher,
        string sessionToken,
        Guid? deviceSessionId,
        Guid hostGameId,
        string gameId,
        string? version) =>
        new(publisher, null, sessionToken, deviceSessionId, hostGameId, gameId, version);
}

/// <summary>
/// Performs the relay room join sequence - room membership, compatibility check, relay ticket and
/// publisher creation - and owns the cleanup of a partially completed join. Extracted from
/// <see cref="GameConnector"/> so that class only installs the resulting publisher.
/// </summary>
internal sealed class RelayRoomConnector(
    IRelayRoomClient roomClient,
    IPublisherFactory publisherFactory,
    ICommandTransportAdapter transportAdapter,
    ILogger logger)
{
    public async Task<RelayConnectionResult> ConnectAsync(
        string roomCode,
        string? sessionToken,
        RelayClientOptions relayOptions,
        CancellationToken cancellationToken)
    {
        string? joinedSessionToken = null;
        Guid? joinedDeviceSessionId = null;
        ITransportPublisher? publisher = null;

        try
        {
            var joinResult = await roomClient.Join(roomCode, sessionToken, cancellationToken);
            if (!joinResult.Success || joinResult.SessionToken is null)
            {
                return RelayConnectionResult.Failed(joinResult.Error ?? new RelayClientError(
                    RelayClientErrorCode.Unknown,
                    "The relay did not return the values required to join."));
            }

            // One hub serves rooms for many games, so a room is only ours when it carries our
            // title and a real host game instance to attribute the host's commands to.
            var gameInfo = joinResult.GameInfo;
            if (!IsCompatibleRoom(gameInfo))
            {
                logger.LogWarning(
                    "Refusing relay room {RoomCode}: it reported game {GameId} hosted by {HostGameId}",
                    roomCode,
                    gameInfo?.Id,
                    gameInfo?.HostId);
                await RemoveMembershipIfJoined(roomCode, joinResult.SessionToken, joinResult.DeviceSessionId);
                return RelayConnectionResult.Failed(new RelayClientError(
                    RelayClientErrorCode.Unknown,
                    "The room was not created by a compatible MakaMek game."));
            }

            joinedSessionToken = joinResult.SessionToken;
            joinedDeviceSessionId = joinResult.DeviceSessionId;

            var (ticket, ticketError) = await GetTicketAsync(
                roomCode, joinResult.SessionToken, relayOptions, cancellationToken);
            if (ticket is null)
            {
                await RemoveMembershipIfJoined(roomCode, joinedSessionToken, joinedDeviceSessionId);
                return RelayConnectionResult.Failed(ticketError!);
            }

            publisher = await publisherFactory.Create(
                new RelayPublisherOptions
                {
                    HubUrl = RelayHubDefaults.BuildHubUrl(relayOptions.BaseUrl),
                    RoomCode = roomCode,
                    RelayTicket = ticket
                },
                cancellationToken);

            // Throw if cancelled; the cancellation catch block below is the single owner of
            // publisher removal and membership cleanup
            cancellationToken.ThrowIfCancellationRequested();

            return RelayConnectionResult.Succeeded(
                publisher,
                joinResult.SessionToken,
                joinResult.DeviceSessionId,
                gameInfo!.HostId,
                gameInfo.Id,
                gameInfo.Version);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await publisher.RemoveAndDisposeAsync(transportAdapter, logger);
            await RemoveMembershipIfJoined(roomCode, joinedSessionToken, joinedDeviceSessionId);
            throw;
        }
        catch (Exception)
        {
            await publisher.RemoveAndDisposeAsync(transportAdapter, logger);
            await RemoveMembershipIfJoined(roomCode, joinedSessionToken, joinedDeviceSessionId);
            return RelayConnectionResult.Failed(new RelayClientError(
                RelayClientErrorCode.NetworkError,
                "Failed to connect the client to the relay."));
        }
    }

    private static bool IsCompatibleRoom(RoomGameInfo? gameInfo) =>
        gameInfo is not null
        && gameInfo.HostId != Guid.Empty
        && string.Equals(gameInfo.Id, RelayGameInfoFactory.GameTitle, StringComparison.Ordinal);

    private async Task<(string? Ticket, RelayClientError? Error)> GetTicketAsync(
        string roomCode,
        string sessionToken,
        RelayClientOptions relayOptions,
        CancellationToken cancellationToken)
    {
        var ticketResult = await roomClient.GetRelayTicket(
            roomCode,
            sessionToken,
            cancellationToken,
            relayOptions);

        if (!ticketResult.Success || string.IsNullOrWhiteSpace(ticketResult.Ticket))
        {
            return (null, ticketResult.Error ?? new RelayClientError(
                RelayClientErrorCode.Unknown,
                "The relay did not issue a relay ticket for the joining session."));
        }

        return (ticketResult.Ticket, null);
    }

    private async Task RemoveMembershipIfJoined(string roomCode, string? sessionToken, Guid? deviceSessionId)
    {
        if (sessionToken is null || deviceSessionId is not { } deviceId) return;

        try
        {
            await roomClient.RemoveMember(roomCode, sessionToken, deviceId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to remove device session from relay room {RoomCode}", roomCode);
        }
    }
}
