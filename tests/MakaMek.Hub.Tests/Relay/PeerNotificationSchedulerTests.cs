using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Sanet.MakaMek.Hub.Relay;
using Sanet.MakaMek.Hub.Rooms;
using Shouldly;
using HubOptions = Sanet.MakaMek.Hub.Configuration.HubOptions;

namespace Sanet.MakaMek.Hub.Tests.Relay;

public class PeerNotificationSchedulerTests
{
    private const string RoomCode = "ROOM1";
    private const string HostConnectionId = "host-conn";
    private static readonly Guid DeviceSessionId = Guid.NewGuid();

    [Fact]
    public void Schedule_WithDelay_AfterAdvance_NotifiesHostWithDeviceSessionId()
    {
        var clock = new FakeTimeProvider();
        var hostClients = CreateHostClients(out var scheduler, clock, delaySeconds: 5);

        scheduler.ScheduleDisconnectNotification(RoomCode, DeviceSessionId);

        hostClients.DidNotReceive().OnPeerDisconnected(Arg.Any<string>());
        clock.Advance(TimeSpan.FromSeconds(5));
        hostClients.Received(1).OnPeerDisconnected(DeviceSessionId.ToString());
    }

    [Fact]
    public void Schedule_WithDelay_AdvanceBeforeDueTime_DoesNotNotify()
    {
        var clock = new FakeTimeProvider();
        var hostClients = CreateHostClients(out var scheduler, clock, delaySeconds: 5);

        scheduler.ScheduleDisconnectNotification(RoomCode, DeviceSessionId);
        clock.Advance(TimeSpan.FromSeconds(4));

        hostClients.DidNotReceive().OnPeerDisconnected(Arg.Any<string>());
    }

    [Fact]
    public void Schedule_ThenCancel_AfterAdvance_DoesNotNotify()
    {
        var clock = new FakeTimeProvider();
        var hostClients = CreateHostClients(out var scheduler, clock, delaySeconds: 5);

        scheduler.ScheduleDisconnectNotification(RoomCode, DeviceSessionId);
        scheduler.CancelDisconnectNotification(RoomCode, DeviceSessionId);
        clock.Advance(TimeSpan.FromSeconds(10));

        hostClients.DidNotReceive().OnPeerDisconnected(Arg.Any<string>());
    }

    [Fact]
    public void Schedule_Twice_AfterAdvance_NotifiesExactlyOnce()
    {
        var clock = new FakeTimeProvider();
        var hostClients = CreateHostClients(out var scheduler, clock, delaySeconds: 5);

        scheduler.ScheduleDisconnectNotification(RoomCode, DeviceSessionId);
        scheduler.ScheduleDisconnectNotification(RoomCode, DeviceSessionId);
        clock.Advance(TimeSpan.FromSeconds(5));

        hostClients.Received(1).OnPeerDisconnected(DeviceSessionId.ToString());
    }

    [Fact]
    public void Schedule_WithZeroDelay_NotifiesImmediately()
    {
        var clock = new FakeTimeProvider();
        var hostClients = CreateHostClients(out var scheduler, clock, delaySeconds: 0);

        scheduler.ScheduleDisconnectNotification(RoomCode, DeviceSessionId);

        hostClients.Received(1).OnPeerDisconnected(DeviceSessionId.ToString());
        scheduler.HasPendingNotification(RoomCode, DeviceSessionId).ShouldBeFalse();
    }

    [Fact]
    public void TimerFires_DeviceReconnected_SkipsNotification()
    {
        var clock = new FakeTimeProvider();
        var roomManager = Substitute.For<IRoomManager>();
        roomManager.GetHostConnectionId(RoomCode).Returns(HostConnectionId);
        var hostClients = CreateHostClients(out var scheduler, clock, delaySeconds: 5, roomManager);
        // The device reconnected before the timer fired.
        roomManager.GetConnectionId(RoomCode, DeviceSessionId).Returns("new-conn");

        scheduler.ScheduleDisconnectNotification(RoomCode, DeviceSessionId);
        clock.Advance(TimeSpan.FromSeconds(5));

        hostClients.DidNotReceive().OnPeerDisconnected(Arg.Any<string>());
        scheduler.HasPendingNotification(RoomCode, DeviceSessionId).ShouldBeFalse();
    }

    [Fact]
    public void Cancel_ScheduledNotification_ClearsPendingEntry()
    {
        var clock = new FakeTimeProvider();
        _ = CreateHostClients(out var scheduler, clock, delaySeconds: 5);

        scheduler.ScheduleDisconnectNotification(RoomCode, DeviceSessionId);
        scheduler.HasPendingNotification(RoomCode, DeviceSessionId).ShouldBeTrue();

        scheduler.CancelDisconnectNotification(RoomCode, DeviceSessionId);
        scheduler.HasPendingNotification(RoomCode, DeviceSessionId).ShouldBeFalse();
    }

    [Fact]
    public void TimerFires_AfterCancellation_SkipsNotification()
    {
        var clock = new ManualTimerProvider();
        var hostClients = CreateHostClients(out var scheduler, clock, delaySeconds: 5);

        scheduler.ScheduleDisconnectNotification(RoomCode, DeviceSessionId);
        scheduler.CancelDisconnectNotification(RoomCode, DeviceSessionId);

        clock.Timer!.Fire();

        hostClients.DidNotReceive().OnPeerDisconnected(Arg.Any<string>());
    }

    [Fact]
    public void TimerFires_FromStaleSchedule_SkipsNotification()
    {
        var clock = new ManualTimerProvider();
        var hostClients = CreateHostClients(out var scheduler, clock, delaySeconds: 5);

        scheduler.ScheduleDisconnectNotification(RoomCode, DeviceSessionId);
        var firstTimer = clock.Timer!;
        scheduler.ScheduleDisconnectNotification(RoomCode, DeviceSessionId);

        firstTimer.Fire();

        hostClients.DidNotReceive().OnPeerDisconnected(Arg.Any<string>());
    }

    [Fact]
    public void Schedule_WithDelay_NoHostConnection_SkipsNotification()
    {
        var clock = new FakeTimeProvider();
        var hostClients = CreateHostClients(out var scheduler, clock, delaySeconds: 5, hostConnectionId: null);

        scheduler.ScheduleDisconnectNotification(RoomCode, DeviceSessionId);
        clock.Advance(TimeSpan.FromSeconds(5));

        hostClients.DidNotReceive().OnPeerDisconnected(Arg.Any<string>());
    }

    [Fact]
    public void TimerFires_HubCallThrows_DoesNotThrow()
    {
        var clock = new FakeTimeProvider();
        var hostClients = CreateHostClients(out var scheduler, clock, delaySeconds: 5);
        hostClients.When(h => h.OnPeerDisconnected(Arg.Any<string>()))
            .Do(_ => throw new InvalidOperationException("notify failed"));

        scheduler.ScheduleDisconnectNotification(RoomCode, DeviceSessionId);
        clock.Advance(TimeSpan.FromSeconds(5));
    }

    private static IRelayHub CreateHostClients(
        out PeerNotificationScheduler scheduler,
        TimeProvider clock,
        int delaySeconds,
        IRoomManager? roomManager = null,
        string? hostConnectionId = HostConnectionId)
    {
        roomManager ??= Substitute.For<IRoomManager>();
        roomManager.GetHostConnectionId(RoomCode).Returns(hostConnectionId);
        roomManager.GetConnectionId(RoomCode, DeviceSessionId).Returns((string?)null);

        var hostClients = Substitute.For<IRelayHub>();
        var hubContext = Substitute.For<IHubContext<RelayHub, IRelayHub>>();
        hubContext.Clients.Client(HostConnectionId).Returns(hostClients);

        var options = Options.Create(new HubOptions { PeerDisconnectNotificationDelaySeconds = delaySeconds });
        scheduler = new PeerNotificationScheduler(
            hubContext, roomManager, clock, options, NullLogger<PeerNotificationScheduler>.Instance);
        return hostClients;
    }

    private sealed class ManualTimerProvider : TimeProvider
    {
        public ManualTimer? Timer { get; private set; }

        public override long GetTimestamp() => 0;

        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UtcNow;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            Timer = new ManualTimer(callback, state);
            return Timer;
        }
    }

    private sealed class ManualTimer : ITimer
    {
        private readonly TimerCallback _callback;
        private readonly object? _state;

        public ManualTimer(TimerCallback callback, object? state)
        {
            _callback = callback;
            _state = state;
        }

        public bool Change(TimeSpan dueTime, TimeSpan period) => true;

        public void Dispose()
        {
        }

        public void Fire() => _callback(_state);

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
