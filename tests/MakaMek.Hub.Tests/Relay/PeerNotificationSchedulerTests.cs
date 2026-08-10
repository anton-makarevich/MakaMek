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

    private static IRelayHub CreateHostClients(
        out PeerNotificationScheduler scheduler,
        TimeProvider clock,
        int delaySeconds,
        IRoomManager? roomManager = null)
    {
        roomManager ??= Substitute.For<IRoomManager>();
        roomManager.GetHostConnectionId(RoomCode).Returns(HostConnectionId);
        roomManager.GetConnectionId(RoomCode, DeviceSessionId).Returns((string?)null);

        var hostClients = Substitute.For<IRelayHub>();
        var hubContext = Substitute.For<IHubContext<RelayHub, IRelayHub>>();
        hubContext.Clients.Client(HostConnectionId).Returns(hostClients);

        var options = Options.Create(new HubOptions { PeerDisconnectNotificationDelaySeconds = delaySeconds });
        scheduler = new PeerNotificationScheduler(
            hubContext, roomManager, clock, options, NullLogger<PeerNotificationScheduler>.Instance);
        return hostClients;
    }
}
