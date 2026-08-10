using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using Sanet.MakaMek.Hub.Rooms;
using HubOptions = Sanet.MakaMek.Hub.Configuration.HubOptions;

namespace Sanet.MakaMek.Hub.Relay;

/// <summary>
/// Coordinates host notifications of peer disconnects. Notifications are deferred by a
/// configurable delay so a brief transport blip that ends in a reconnect of the same
/// device session does not produce a spurious disconnect notification to the host.
/// </summary>
public interface IPeerNotificationScheduler
{
    void ScheduleDisconnectNotification(string roomCode, Guid deviceSessionId);
    void CancelDisconnectNotification(string roomCode, Guid deviceSessionId);
}

/// <summary>
/// Thread-safe scheduler that defers <see cref="IRelayHub.OnPeerDisconnected"/> delivery
/// to the current host. A reconnect cancels the pending notification for the device session.
/// A configured delay of zero reproduces the immediate-notification behavior.
/// </summary>
public sealed class PeerNotificationScheduler : IPeerNotificationScheduler
{
    private readonly IHubContext<RelayHub, IRelayHub> _hubContext;
    private readonly IRoomManager _roomManager;
    private readonly TimeProvider _timeProvider;
    private readonly HubOptions _options;
    private readonly ILogger<PeerNotificationScheduler> _logger;

    private readonly Lock _sync = new();
    private readonly Dictionary<(string RoomCode, Guid DeviceSessionId), ITimer> _pending = new();

    public PeerNotificationScheduler(
        IHubContext<RelayHub, IRelayHub> hubContext,
        IRoomManager roomManager,
        TimeProvider timeProvider,
        IOptions<HubOptions> options,
        ILogger<PeerNotificationScheduler> logger)
    {
        _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
        _roomManager = roomManager ?? throw new ArgumentNullException(nameof(roomManager));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
        _logger = logger;
    }

    public void ScheduleDisconnectNotification(string roomCode, Guid deviceSessionId)
    {
        var key = (roomCode, deviceSessionId);

        lock (_sync)
        {
            CancelLocked(key);

            var delaySeconds = _options.PeerDisconnectNotificationDelaySeconds;
            if (delaySeconds == 0)
            {
                _ = NotifyHostOfPeerDisconnectAsync(roomCode, deviceSessionId);
                return;
            }

            ITimer? timer = null;
            timer = _timeProvider.CreateTimer(
                _ =>
                {
                    lock (_sync)
                    {
                        if (!_pending.TryGetValue(key, out var pending) || !ReferenceEquals(pending, timer))
                        {
                            return;
                        }

                        _pending.Remove(key);
                        pending.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                        pending.Dispose();
                    }

                    _ = NotifyHostOfPeerDisconnectAsync(roomCode, deviceSessionId);
                },
                null,
                TimeSpan.FromSeconds(delaySeconds),
                Timeout.InfiniteTimeSpan);

            _pending[key] = timer;
        }
    }

    public void CancelDisconnectNotification(string roomCode, Guid deviceSessionId)
    {
        lock (_sync)
        {
            CancelLocked((roomCode, deviceSessionId));
        }
    }

    private void CancelLocked((string RoomCode, Guid DeviceSessionId) key)
    {
        if (!_pending.TryGetValue(key, out var timer))
        {
            return;
        }

        _pending.Remove(key);
        timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        timer.Dispose();
    }

    private async Task NotifyHostOfPeerDisconnectAsync(string roomCode, Guid deviceSessionId)
    {
        try
        {
            // A reconnect during the delay leaves an active connection for the device,
            // which makes the pending notification obsolete.
            if (_roomManager.GetConnectionId(roomCode, deviceSessionId) is not null)
            {
                _logger.LogDebug(
                    "Peer disconnect notification for device session {DeviceSessionId} in room {RoomCode} suppressed: device reconnected",
                    deviceSessionId,
                    roomCode);
                return;
            }

            var hostConnectionId = _roomManager.GetHostConnectionId(roomCode);
            if (hostConnectionId is null)
            {
                _logger.LogDebug(
                    "Peer disconnect notification for device session {DeviceSessionId} in room {RoomCode} skipped: no host connection",
                    deviceSessionId,
                    roomCode);
                return;
            }

            await _hubContext.Clients.Client(hostConnectionId)
                .OnPeerDisconnected(deviceSessionId.ToString());
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Failed to notify host of peer disconnect for device session {DeviceSessionId} in room {RoomCode}",
                deviceSessionId,
                roomCode);
        }
    }

    internal bool HasPendingNotification(string roomCode, Guid deviceSessionId)
    {
        lock (_sync)
        {
            return _pending.ContainsKey((roomCode, deviceSessionId));
        }
    }
}
