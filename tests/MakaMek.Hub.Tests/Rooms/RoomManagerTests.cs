using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Sanet.MakaMek.Hub.Configuration;
using Sanet.MakaMek.Hub.Rooms;
using Sanet.MakaMek.Hub.Tests.TestLoggers;
using Shouldly;

namespace Sanet.MakaMek.Hub.Tests.Rooms;

public class RoomManagerTests
{
    private const int DefaultRoomTtlSeconds = 7200;
    private const int DefaultDissolutionGracePeriodSeconds = 30;
    [Fact]
    public void CreateRoom_CreatesHostDeviceSessionAndTwoHourExpiry()
    {
        var now = new DateTimeOffset(2026, 7, 20, 12, 0, 0, TimeSpan.Zero);
        var hostGameId = Guid.NewGuid();
        var manager = CreateManager(
            new SequenceRoomCodeGenerator("ABC234"),
            now: now);

        var result = manager.CreateRoom(hostGameId);

        result.Outcome.ShouldBe(RoomCreationOutcome.Created);
        result.ActiveRoomCount.ShouldBe(1);
        result.Room.ShouldNotBeNull();
        result.Session.ShouldNotBeNull();
        result.Room!.RoomCode.ShouldBe("ABC234");
        result.Room.HostGameId.ShouldBe(hostGameId);
        result.Room.HostDeviceSessionId.ShouldBe(result.Session!.DeviceSessionId);
        result.Room.ExpiresAt.ShouldBe(now.AddSeconds(DefaultRoomTtlSeconds));
        result.Room.Members.Count.ShouldBe(1);
        var host = result.Room.Members.Single();
        host.DeviceSessionId.ShouldBe(result.Session.DeviceSessionId);
        host.Role.ShouldBe(RoomRole.Host);
        result.Session.DeviceSessionId.ShouldNotBe(Guid.Empty);
        result.Session.Role.ShouldBe(RoomRole.Host);
        result.Session.RoomCode.ShouldBe("ABC234");
        result.Session.ExpiresAt.ShouldBe(now.AddSeconds(DefaultRoomTtlSeconds));
        string.IsNullOrWhiteSpace(result.Session.Token).ShouldBeFalse();
    }

    [Fact]
    public void CreateRoom_WhenGeneratedCodeCollides_RetriesUntilItFindsAnAvailableCode()
    {
        var generator = new SequenceRoomCodeGenerator("ABC234", "ABC234", "DEF567");
        var manager = CreateManager(generator);

        var first = manager.CreateRoom(Guid.NewGuid());
        var second = manager.CreateRoom(Guid.NewGuid());

        first.Room!.RoomCode.ShouldBe("ABC234");
        second.Room!.RoomCode.ShouldBe("DEF567");
        generator.GeneratedCount.ShouldBe(3);
    }

    [Fact]
    public void CreateRoom_WhenAtCapacity_ReturnsCurrentActiveRoomCount()
    {
        var manager = CreateManager(
            new SequenceRoomCodeGenerator("ABC234", "DEF567"),
            maxConcurrentRooms: 1);

        manager.CreateRoom(Guid.NewGuid());
        var result = manager.CreateRoom(Guid.NewGuid());

        result.Outcome.ShouldBe(RoomCreationOutcome.HubAtCapacity);
        result.ActiveRoomCount.ShouldBe(1);
        result.Room.ShouldBeNull();
        result.Session.ShouldBeNull();
    }

    [Fact]
    public void CreateRoom_WithEmptyGameId_ThrowsArgumentException()
    {
        var manager = CreateManager(new SequenceRoomCodeGenerator("ABC234"));

        Should.Throw<ArgumentException>(() => manager.CreateRoom(Guid.Empty));
    }

    [Fact]
    public void CreateRoom_AfterExpiredRoomsAreCleanedUp_Succeeds()
    {
        var now = new DateTimeOffset(2026, 7, 20, 12, 0, 0, TimeSpan.Zero);
        var generator = new SequenceRoomCodeGenerator("ABC234", "DEF567");
        var timeProvider = new FixedTimeProvider(now);
        var manager = CreateManager(generator, maxConcurrentRooms: 1, timeProvider: timeProvider);

        manager.CreateRoom(Guid.NewGuid());

        timeProvider.Advance(TimeSpan.FromSeconds(DefaultRoomTtlSeconds).Add(TimeSpan.FromMinutes(1)));

        var result = manager.CreateRoom(Guid.NewGuid());

        result.Outcome.ShouldBe(RoomCreationOutcome.Created);
        result.Room.ShouldNotBeNull();
        result.Room!.RoomCode.ShouldBe("DEF567");
        result.ActiveRoomCount.ShouldBe(1);
    }

    [Fact]
    public void CreateRoom_WhenAllGeneratedCodesCollide_ThrowsInvalidOperationException()
    {
        var alwaysSame = new AlwaysSameCodeGenerator("DUP");
        var manager = CreateManager(alwaysSame);

        manager.CreateRoom(Guid.NewGuid());

        var ex = Should.Throw<InvalidOperationException>(
            () => manager.CreateRoom(Guid.NewGuid()));

        ex.Message.ShouldBe("Unable to generate a unique room code.");
        alwaysSame.GeneratedCount.ShouldBe(129);
    }

    [Fact]
    public void Generate_ReturnsSixUnambiguousCharacters()
    {
        var generator = new CryptographicRoomCodeGenerator();

        for (var attempt = 0; attempt < 100; attempt++)
        {
            var roomCode = generator.Generate();

            roomCode.Length.ShouldBe(CryptographicRoomCodeGenerator.CodeLength);
            roomCode.All("ABCDEFGHJKMNPQRSTUVWXYZ23456789".Contains).ShouldBeTrue();
        }
    }

    [Fact]
    public void JoinRoom_NotFound_ReturnsNotFound()
    {
        var manager = CreateManager(new SequenceRoomCodeGenerator("ABC234"));

        var result = manager.JoinRoom("NOEXIST", sessionToken: null);

        result.Outcome.ShouldBe(RoomJoinOutcome.RoomNotFound);
        result.Room.ShouldBeNull();
        result.Session.ShouldBeNull();
    }

    [Fact]
    public void JoinRoom_ExpiredRoom_ReturnsExpired()
    {
        var now = new DateTimeOffset(2026, 7, 20, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new FixedTimeProvider(now);
        var manager = CreateManager(
            new SequenceRoomCodeGenerator("ABC234"),
            timeProvider: timeProvider);

        manager.CreateRoom(Guid.NewGuid());

        timeProvider.Advance(TimeSpan.FromSeconds(DefaultRoomTtlSeconds).Add(TimeSpan.FromMinutes(1)));

        var result = manager.JoinRoom("ABC234", sessionToken: null);

        result.Outcome.ShouldBe(RoomJoinOutcome.RoomExpired);
    }

    [Fact]
    public void JoinRoom_NotReady_ReturnsNotReady()
    {
        var manager = CreateManager(new SequenceRoomCodeGenerator("ABC234"));

        manager.CreateRoom(Guid.NewGuid());

        var result = manager.JoinRoom("ABC234", sessionToken: null);

        result.Outcome.ShouldBe(RoomJoinOutcome.HostNotReady);
    }

    [Fact]
    public void JoinRoom_ReadyRoom_MintsDeviceSessionAndIssuesSession()
    {
        var now = new DateTimeOffset(2026, 7, 20, 12, 0, 0, TimeSpan.Zero);
        var manager = CreateManager(
            new SequenceRoomCodeGenerator("ABC234"),
            now: now);

        var createResult = manager.CreateRoom(Guid.NewGuid());
        manager.MarkRoomReady("ABC234", createResult.Session!.Token);

        var result = manager.JoinRoom("ABC234", sessionToken: null);

        result.Outcome.ShouldBe(RoomJoinOutcome.Joined);
        result.Room.ShouldNotBeNull();
        result.Session.ShouldNotBeNull();
        result.Room!.RoomCode.ShouldBe("ABC234");
        result.Room.Members.Count.ShouldBe(2);
        result.Session!.DeviceSessionId.ShouldNotBe(Guid.Empty);
        result.Session.DeviceSessionId.ShouldNotBe(createResult.Session.DeviceSessionId);
        result.Session.Role.ShouldBe(RoomRole.Client);
        result.Session.RoomCode.ShouldBe("ABC234");
        result.Session.ExpiresAt.ShouldBe(now.AddSeconds(DefaultRoomTtlSeconds));
        string.IsNullOrWhiteSpace(result.Session.Token).ShouldBeFalse();
    }

    [Fact]
    public void JoinRoom_RejoinWithValidSessionToken_ReusesSameDeviceSession()
    {
        var now = new DateTimeOffset(2026, 7, 20, 12, 0, 0, TimeSpan.Zero);
        var manager = CreateManager(
            new SequenceRoomCodeGenerator("ABC234"),
            now: now);

        var createResult = manager.CreateRoom(Guid.NewGuid());
        manager.MarkRoomReady("ABC234", createResult.Session!.Token);

        var first = manager.JoinRoom("ABC234", sessionToken: null);
        var firstDeviceSessionId = first.Session!.DeviceSessionId;

        var result = manager.JoinRoom("ABC234", sessionToken: first.Session.Token);

        result.Outcome.ShouldBe(RoomJoinOutcome.Joined);
        result.Room!.Members.Count.ShouldBe(2);
        result.Session!.DeviceSessionId.ShouldBe(firstDeviceSessionId);
        result.Session.Token.ShouldNotBe(first.Session.Token);
    }

    [Fact]
    public void JoinRoom_SessionExpiresAtRoomExpiry()
    {
        var now = new DateTimeOffset(2026, 7, 20, 12, 0, 0, TimeSpan.Zero);
        var manager = CreateManager(
            new SequenceRoomCodeGenerator("ABC234"),
            now: now);

        var createResult = manager.CreateRoom(Guid.NewGuid());
        manager.MarkRoomReady("ABC234", createResult.Session!.Token);

        var result = manager.JoinRoom("ABC234", sessionToken: null);

        result.Session!.ExpiresAt.ShouldBe(result.Room!.ExpiresAt);
    }

    [Fact]
    public void MarkRoomReady_HostMarksReady_Succeeds()
    {
        var now = new DateTimeOffset(2026, 7, 20, 12, 0, 0, TimeSpan.Zero);
        var manager = CreateManager(
            new SequenceRoomCodeGenerator("ABC234"),
            now: now);

        var createResult = manager.CreateRoom(Guid.NewGuid());

        var result = manager.MarkRoomReady("ABC234", createResult.Session!.Token);

        result.Outcome.ShouldBe(RoomReadyOutcome.Ready);
    }

    [Fact]
    public void MarkRoomReady_NonHost_ReturnsNotHost()
    {
        var manager = CreateManager(new SequenceRoomCodeGenerator("ABC234"));

        var createResult = manager.CreateRoom(Guid.NewGuid());

        var result = manager.MarkRoomReady("ABC234", "invalid-token");

        result.Outcome.ShouldBe(RoomReadyOutcome.NotHost);
    }

    [Fact]
    public void MarkRoomReady_NotFound_ReturnsNotFound()
    {
        var manager = CreateManager(new SequenceRoomCodeGenerator("ABC234"));

        var result = manager.MarkRoomReady("NOEXIST", "any-token");

        result.Outcome.ShouldBe(RoomReadyOutcome.RoomNotFound);
    }

    [Fact]
    public void MarkRoomReady_ExpiredRoom_ReturnsExpired()
    {
        var now = new DateTimeOffset(2026, 7, 20, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new FixedTimeProvider(now);
        var manager = CreateManager(
            new SequenceRoomCodeGenerator("ABC234"),
            timeProvider: timeProvider);

        var createResult = manager.CreateRoom(Guid.NewGuid());

        timeProvider.Advance(TimeSpan.FromSeconds(DefaultRoomTtlSeconds).Add(TimeSpan.FromMinutes(1)));

        var result = manager.MarkRoomReady("ABC234", createResult.Session!.Token);

        result.Outcome.ShouldBe(RoomReadyOutcome.RoomExpired);
    }

    [Fact]
    public void MarkRoomReady_WhenRoomAlreadyActive_ReturnsInvalidRoomState()
    {
        var manager = CreateManager(new SequenceRoomCodeGenerator("ABC234"));
        var createResult = manager.CreateRoom(Guid.NewGuid());
        manager.MarkRoomReady("ABC234", createResult.Session!.Token);

        var result = manager.MarkRoomReady("ABC234", createResult.Session.Token);

        result.Outcome.ShouldBe(RoomReadyOutcome.InvalidRoomState);
        createResult.Room!.State.ShouldBe(RoomState.Active);
    }

    [Fact]
    public void CloseRoom_ActiveRoom_TransitionsToClosed()
    {
        var manager = CreateManager(new SequenceRoomCodeGenerator("ABC234"));
        var createResult = manager.CreateRoom(Guid.NewGuid());
        manager.MarkRoomReady("ABC234", createResult.Session!.Token);

        var result = manager.CloseRoom("ABC234", createResult.Session.Token);

        result.Outcome.ShouldBe(RoomCloseOutcome.Closed);
        createResult.Room!.State.ShouldBe(RoomState.Closed);
    }

    [Fact]
    public void CloseRoom_NotFound_ReturnsNotFound()
    {
        var manager = CreateManager(new SequenceRoomCodeGenerator("ABC234"));

        var result = manager.CloseRoom("NOEXIST", "any-token");

        result.Outcome.ShouldBe(RoomCloseOutcome.RoomNotFound);
    }

    [Fact]
    public void CloseRoom_ExpiredRoom_ReturnsExpired()
    {
        var now = new DateTimeOffset(2026, 7, 20, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new FixedTimeProvider(now);
        var manager = CreateManager(
            new SequenceRoomCodeGenerator("ABC234"),
            timeProvider: timeProvider);

        var createResult = manager.CreateRoom(Guid.NewGuid());
        manager.MarkRoomReady("ABC234", createResult.Session!.Token);

        timeProvider.Advance(TimeSpan.FromSeconds(DefaultRoomTtlSeconds).Add(TimeSpan.FromMinutes(1)));

        var result = manager.CloseRoom("ABC234", createResult.Session.Token);

        result.Outcome.ShouldBe(RoomCloseOutcome.RoomExpired);
    }

    [Fact]
    public void CloseRoom_NonHost_ReturnsNotHost()
    {
        var manager = CreateManager(new SequenceRoomCodeGenerator("ABC234"));
        var createResult = manager.CreateRoom(Guid.NewGuid());
        manager.MarkRoomReady("ABC234", createResult.Session!.Token);

        var result = manager.CloseRoom("ABC234", "not-the-host-token");

        result.Outcome.ShouldBe(RoomCloseOutcome.NotHost);
        createResult.Room!.State.ShouldBe(RoomState.Active);
    }

    [Fact]
    public void CloseRoom_WhenRoomNotActive_ReturnsInvalidRoomState()
    {
        var manager = CreateManager(new SequenceRoomCodeGenerator("ABC234"));
        var createResult = manager.CreateRoom(Guid.NewGuid());

        var result = manager.CloseRoom("ABC234", createResult.Session!.Token);

        result.Outcome.ShouldBe(RoomCloseOutcome.InvalidRoomState);
        createResult.Room!.State.ShouldBe(RoomState.Created);
    }

    [Fact]
    public void JoinRoom_ClosedRoom_NewDevice_ReturnsRoomFull()
    {
        var manager = CreateManager(new SequenceRoomCodeGenerator("ABC234"));
        var createResult = manager.CreateRoom(Guid.NewGuid());
        manager.MarkRoomReady("ABC234", createResult.Session!.Token);
        manager.CloseRoom("ABC234", createResult.Session.Token);

        var result = manager.JoinRoom("ABC234", sessionToken: null);

        result.Outcome.ShouldBe(RoomJoinOutcome.RoomFull);
        createResult.Room!.State.ShouldBe(RoomState.Closed);
        createResult.Room.Members.Count.ShouldBe(1);
    }

    [Fact]
    public void JoinRoom_ClosedRoom_ExistingDeviceSession_ReturnsJoined()
    {
        var manager = CreateManager(new SequenceRoomCodeGenerator("ABC234"));
        var createResult = manager.CreateRoom(Guid.NewGuid());
        manager.MarkRoomReady("ABC234", createResult.Session!.Token);

        var joined = manager.JoinRoom("ABC234", sessionToken: null);
        manager.CloseRoom("ABC234", createResult.Session!.Token);

        var result = manager.JoinRoom("ABC234", sessionToken: joined.Session!.Token);

        result.Outcome.ShouldBe(RoomJoinOutcome.Joined);
        result.Session.ShouldNotBeNull();
        result.Session!.DeviceSessionId.ShouldBe(joined.Session.DeviceSessionId);
        createResult.Room!.State.ShouldBe(RoomState.Closed);
    }

    [Fact]
    public void RemoveMember_RemovesRosterEntryAndRevokesSessions()
    {
        var manager = CreateManager(new SequenceRoomCodeGenerator("ABC234"));
        var createResult = manager.CreateRoom(Guid.NewGuid());
        manager.MarkRoomReady("ABC234", createResult.Session!.Token);

        var joined = manager.JoinRoom("ABC234", sessionToken: null);
        var clientDeviceSessionId = joined.Session!.DeviceSessionId;
        var clientToken = joined.Session.Token;

        createResult.Room!.HasSession(clientToken).ShouldBeTrue();

        var result = manager.RemoveMember("ABC234", createResult.Session!.Token, clientDeviceSessionId);

        result.Outcome.ShouldBe(RoomRemoveMemberOutcome.Removed);
        createResult.Room.IsMember(clientDeviceSessionId).ShouldBeFalse();
        createResult.Room.HasSession(clientToken).ShouldBeFalse();
        createResult.Room.Members.Count.ShouldBe(1);
    }

    [Fact]
    public void RemoveMember_UnknownDeviceSession_ReturnsMemberNotFound()
    {
        var manager = CreateManager(new SequenceRoomCodeGenerator("ABC234"));
        var createResult = manager.CreateRoom(Guid.NewGuid());
        manager.MarkRoomReady("ABC234", createResult.Session!.Token);

        var result = manager.RemoveMember("ABC234", createResult.Session!.Token, Guid.NewGuid());

        result.Outcome.ShouldBe(RoomRemoveMemberOutcome.MemberNotFound);
        createResult.Room!.Members.Count.ShouldBe(1);
    }

    [Fact]
    public void RemoveMember_HostDeviceSession_ReturnsCannotRemoveHost()
    {
        var manager = CreateManager(new SequenceRoomCodeGenerator("ABC234"));
        var createResult = manager.CreateRoom(Guid.NewGuid());
        manager.MarkRoomReady("ABC234", createResult.Session!.Token);
        var hostDeviceSessionId = createResult.Session!.DeviceSessionId;

        var result = manager.RemoveMember("ABC234", createResult.Session.Token, hostDeviceSessionId);

        result.Outcome.ShouldBe(RoomRemoveMemberOutcome.CannotRemoveHost);
        createResult.Room!.IsMember(hostDeviceSessionId).ShouldBeTrue();
    }

    [Fact]
    public void RemoveMember_NonHost_ReturnsNotHost()
    {
        var manager = CreateManager(new SequenceRoomCodeGenerator("ABC234"));
        var createResult = manager.CreateRoom(Guid.NewGuid());
        manager.MarkRoomReady("ABC234", createResult.Session!.Token);

        var joined = manager.JoinRoom("ABC234", sessionToken: null);
        var clientDeviceSessionId = joined.Session!.DeviceSessionId;

        var result = manager.RemoveMember("ABC234", "not-the-host-token", clientDeviceSessionId);

        result.Outcome.ShouldBe(RoomRemoveMemberOutcome.NotHost);
        createResult.Room!.IsMember(clientDeviceSessionId).ShouldBeTrue();
    }

    [Fact]
    public void RemoveMember_RoomNotFound_ReturnsNotFound()
    {
        var manager = CreateManager(new SequenceRoomCodeGenerator("ABC234"));

        var result = manager.RemoveMember("NOEXIST", "any-token", Guid.NewGuid());

        result.Outcome.ShouldBe(RoomRemoveMemberOutcome.RoomNotFound);
    }

    [Fact]
    public void RemoveMember_ExpiredRoom_ReturnsExpired()
    {
        var now = new DateTimeOffset(2026, 7, 20, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new FixedTimeProvider(now);
        var manager = CreateManager(
            new SequenceRoomCodeGenerator("ABC234"),
            timeProvider: timeProvider);

        var createResult = manager.CreateRoom(Guid.NewGuid());
        manager.MarkRoomReady("ABC234", createResult.Session!.Token);

        var joined = manager.JoinRoom("ABC234", sessionToken: null);

        timeProvider.Advance(TimeSpan.FromSeconds(DefaultRoomTtlSeconds).Add(TimeSpan.FromMinutes(1)));

        var result = manager.RemoveMember("ABC234", createResult.Session!.Token, joined.Session!.DeviceSessionId);

        result.Outcome.ShouldBe(RoomRemoveMemberOutcome.RoomExpired);
    }

    [Fact]
    public void AuthenticateSession_WithValidHostToken_ReturnsBoundSession()
    {
        var manager = CreateManager(new SequenceRoomCodeGenerator("ABC234"));
        var created = manager.CreateRoom(Guid.NewGuid());

        var session = manager.AuthenticateSession(created.Session!.Token);

        session.ShouldNotBeNull();
        session.RoomCode.ShouldBe("ABC234");
        session.DeviceSessionId.ShouldBe(created.Session.DeviceSessionId);
        session.Role.ShouldBe(RoomRole.Host);
        session.Token.ShouldBe(created.Session.Token);
    }

    [Fact]
    public void AuthenticateSession_WithValidClientToken_ReturnsBoundSession()
    {
        var manager = CreateManager(new SequenceRoomCodeGenerator("ABC234"));
        var created = manager.CreateRoom(Guid.NewGuid());
        manager.MarkRoomReady("ABC234", created.Session!.Token);
        var joined = manager.JoinRoom("ABC234", sessionToken: null);

        var session = manager.AuthenticateSession(joined.Session!.Token);

        session.ShouldNotBeNull();
        session.Role.ShouldBe(RoomRole.Client);
        session.RoomCode.ShouldBe("ABC234");
        session.DeviceSessionId.ShouldBe(joined.Session.DeviceSessionId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("unknown-token")]
    public void AuthenticateSession_WithMissingOrUnknownToken_ReturnsNull(string? token)
    {
        var manager = CreateManager(new SequenceRoomCodeGenerator("ABC234"));
        manager.CreateRoom(Guid.NewGuid());

        manager.AuthenticateSession(token!).ShouldBeNull();
    }

    [Fact]
    public void AuthenticateSession_WithExpiredToken_ReturnsNull()
    {
        var now = new DateTimeOffset(2026, 7, 23, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new FixedTimeProvider(now);
        var manager = CreateManager(
            new SequenceRoomCodeGenerator("ABC234"),
            timeProvider: timeProvider);
        var created = manager.CreateRoom(Guid.NewGuid());

        timeProvider.Advance(TimeSpan.FromSeconds(DefaultRoomTtlSeconds).Add(TimeSpan.FromMinutes(1)));

        manager.AuthenticateSession(created.Session!.Token).ShouldBeNull();
    }

    [Fact]
    public void AuthenticateSession_WithRevokedClientToken_ReturnsNull()
    {
        var manager = CreateManager(new SequenceRoomCodeGenerator("ABC234"));
        var created = manager.CreateRoom(Guid.NewGuid());
        manager.MarkRoomReady("ABC234", created.Session!.Token);
        var joined = manager.JoinRoom("ABC234", sessionToken: null);

        manager.RemoveMember("ABC234", created.Session!.Token, joined.Session!.DeviceSessionId);

        manager.AuthenticateSession(joined.Session.Token).ShouldBeNull();
    }

    [Fact]
    public void AuthenticateSession_WhenSessionExpiredButRoomExtended_ReturnsNull()
    {
        var now = new DateTimeOffset(2026, 7, 23, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new FixedTimeProvider(now);
        var manager = CreateManager(
            new SequenceRoomCodeGenerator("ABC234"),
            timeProvider: timeProvider);
        var created = manager.CreateRoom(Guid.NewGuid());
        manager.MarkRoomReady("ABC234", created.Session!.Token);
        var clientA = manager.JoinRoom("ABC234", sessionToken: null);

        // Almost a full TTL later, a second member joins, extending the room's
        // expiry past the point where client A's session has already lapsed.
        timeProvider.Advance(TimeSpan.FromSeconds(DefaultRoomTtlSeconds - 1));
        manager.JoinRoom("ABC234", sessionToken: null);

        timeProvider.Advance(TimeSpan.FromSeconds(2));

        manager.AuthenticateSession(clientA.Session!.Token).ShouldBeNull();
    }

    [Fact]
    public void AuthenticateSession_WithClosedRoomToken_ReturnsBoundSession()
    {
        var manager = CreateManager(new SequenceRoomCodeGenerator("ABC234"));
        var created = manager.CreateRoom(Guid.NewGuid());
        manager.MarkRoomReady("ABC234", created.Session!.Token);
        manager.CloseRoom("ABC234", created.Session.Token);

        manager.AuthenticateSession(created.Session.Token).ShouldBe(created.Session);
    }

    [Fact]
    public void Connections_RegisterReplaceUnregisterAndFindHost()
    {
        var manager = CreateManager(new SequenceRoomCodeGenerator("ABC234"));
        var created = manager.CreateRoom(Guid.NewGuid());
        var hostDeviceSessionId = created.Session!.DeviceSessionId;

        manager.RegisterConnection("ABC234", hostDeviceSessionId, "host-old").ShouldBeNull();
        manager.GetHostConnectionId("ABC234").ShouldBe("host-old");
        manager.RegisterConnection("ABC234", hostDeviceSessionId, "host-new").ShouldBe("host-old");
        manager.UnregisterConnection("ABC234", hostDeviceSessionId, "host-old").ShouldBeFalse();
        manager.UnregisterConnection("ABC234", hostDeviceSessionId, "host-new").ShouldBeTrue();
        manager.GetHostConnectionId("ABC234").ShouldBeNull();
    }

    [Fact]
    public void HostAndJoiningDevice_RegisterExactlyTwoDeviceSessionsAndConnections()
    {
        var manager = CreateManager(new SequenceRoomCodeGenerator("ABC234"));
        var hostGameId = Guid.NewGuid();

        var created = manager.CreateRoom(hostGameId);
        var hostDeviceSessionId = created.Session!.DeviceSessionId;
        manager.MarkRoomReady("ABC234", created.Session!.Token);

        var joined = manager.JoinRoom("ABC234", sessionToken: null);
        var clientDeviceSessionId = joined.Session!.DeviceSessionId;

        manager.RegisterConnection("ABC234", hostDeviceSessionId, "host-conn");
        manager.RegisterConnection("ABC234", clientDeviceSessionId, "client-conn");

        var room = created.Room!;
        // Exactly two device sessions: two members, two sessions, two connections.
        room.Members.Count.ShouldBe(2);
        room.Members.Select(m => m.DeviceSessionId).ShouldBe(
            new[] { hostDeviceSessionId, clientDeviceSessionId }, ignoreOrder: true);
        room.Members.Select(m => m.DeviceSessionId).Distinct().Count().ShouldBe(2);
        room.HasSession(created.Session.Token).ShouldBeTrue();
        room.HasSession(joined.Session!.Token).ShouldBeTrue();
        room.LiveConnectionCount.ShouldBe(2);

        // Hub state exposes device sessions and connections only. Membership carries
        // no player identity, and the two protected relay channels are the only
        // connections for the whole session regardless of how many players participate;
        // player participation flows only through the game command stream over these
        // two device channels and produces no additional Hub device sessions.
        room.Members.All(m => m.Role is RoomRole.Host or RoomRole.Client).ShouldBeTrue();
        room.Members.All(m => m.DeviceSessionId != Guid.Empty).ShouldBeTrue();
    }

    [Fact]
    public void Dissolution_AfterGrace_RemovesRoomAndRejectsSession()
    {
        var now = new DateTimeOffset(2026, 7, 23, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new FixedTimeProvider(now);
        var manager = CreateManager(new SequenceRoomCodeGenerator("ABC234"), timeProvider: timeProvider);
        var created = manager.CreateRoom(Guid.NewGuid());
        manager.MarkRoomReady("ABC234", created.Session!.Token);
        manager.MarkRoomForDissolution("ABC234");

        timeProvider.Advance(TimeSpan.FromSeconds(DefaultDissolutionGracePeriodSeconds));

        manager.JoinRoom("ABC234", sessionToken: null).Outcome.ShouldBe(RoomJoinOutcome.RoomNotFound);
        manager.AuthenticateSession(created.Session.Token).ShouldBeNull();
    }

    [Fact]
    public void CancelDissolution_AfterDeadline_DoesNotResurrectRoom()
    {
        var timeProvider = new FixedTimeProvider(new DateTimeOffset(2026, 7, 23, 12, 0, 0, TimeSpan.Zero));
        var manager = CreateManager(new SequenceRoomCodeGenerator("ABC234"), timeProvider: timeProvider);
        var created = manager.CreateRoom(Guid.NewGuid());
        manager.MarkRoomReady("ABC234", created.Session!.Token);
        manager.MarkRoomForDissolution("ABC234");

        timeProvider.Advance(TimeSpan.FromSeconds(DefaultDissolutionGracePeriodSeconds));

        manager.CancelRoomDissolution("ABC234");

        manager.AuthenticateSession(created.Session!.Token).ShouldBeNull();
        manager.JoinRoom("ABC234", sessionToken: null).Outcome.ShouldBe(RoomJoinOutcome.RoomNotFound);
    }

    [Fact]
    public void MarkRoomForDissolution_AlreadyDissolved_PurgesRoom()
    {
        var timeProvider = new FixedTimeProvider(new DateTimeOffset(2026, 7, 23, 12, 0, 0, TimeSpan.Zero));
        var manager = CreateManager(new SequenceRoomCodeGenerator("ABC234"), timeProvider: timeProvider);
        var created = manager.CreateRoom(Guid.NewGuid());
        manager.MarkRoomReady("ABC234", created.Session!.Token);
        manager.MarkRoomForDissolution("ABC234");

        timeProvider.Advance(TimeSpan.FromSeconds(DefaultDissolutionGracePeriodSeconds));

        // Re-marking a dissolved room purges it instead of setting a new deadline.
        manager.MarkRoomForDissolution("ABC234");

        manager.AuthenticateSession(created.Session!.Token).ShouldBeNull();
        manager.JoinRoom("ABC234", sessionToken: null).Outcome.ShouldBe(RoomJoinOutcome.RoomNotFound);
    }

    [Fact]
    public void JoinRoom_DissolvedRoom_ReturnsNotFound()
    {
        var timeProvider = new FixedTimeProvider(new DateTimeOffset(2026, 7, 23, 12, 0, 0, TimeSpan.Zero));
        var manager = CreateManager(new SequenceRoomCodeGenerator("ABC234"), timeProvider: timeProvider);
        var created = manager.CreateRoom(Guid.NewGuid());
        manager.MarkRoomReady("ABC234", created.Session!.Token);
        manager.MarkRoomForDissolution("ABC234");

        timeProvider.Advance(TimeSpan.FromSeconds(DefaultDissolutionGracePeriodSeconds));

        manager.JoinRoom("ABC234", sessionToken: null).Outcome.ShouldBe(RoomJoinOutcome.RoomNotFound);
    }

    [Fact]
    public void TryMarkHostDisconnected_SupersededByNewConnection_ReturnsFalse()
    {
        var timeProvider = new FixedTimeProvider(new DateTimeOffset(2026, 7, 23, 12, 0, 0, TimeSpan.Zero));
        var manager = CreateManager(new SequenceRoomCodeGenerator("ABC234"), timeProvider: timeProvider);
        var created = manager.CreateRoom(Guid.NewGuid());
        var hostDeviceSessionId = created.Session!.DeviceSessionId;
        manager.RegisterConnection("ABC234", hostDeviceSessionId, "host-old");
        manager.RegisterConnection("ABC234", hostDeviceSessionId, "host-new");

        // Old disconnect is superseded — should not mark dissolution.
        var marked = manager.TryMarkHostDisconnected("ABC234", hostDeviceSessionId, "host-old");

        marked.ShouldBeFalse();
        manager.GetHostConnectionId("ABC234").ShouldBe("host-new");
    }

    [Fact]
    public void TryMarkHostDisconnected_LastConnection_MarksDissolution()
    {
        var timeProvider = new FixedTimeProvider(new DateTimeOffset(2026, 7, 23, 12, 0, 0, TimeSpan.Zero));
        var manager = CreateManager(new SequenceRoomCodeGenerator("ABC234"), timeProvider: timeProvider);
        var created = manager.CreateRoom(Guid.NewGuid());
        var hostDeviceSessionId = created.Session!.DeviceSessionId;
        manager.RegisterConnection("ABC234", hostDeviceSessionId, "host-only");

        var marked = manager.TryMarkHostDisconnected("ABC234", hostDeviceSessionId, "host-only");

        marked.ShouldBeTrue();
        manager.GetHostConnectionId("ABC234").ShouldBeNull();
    }

    [Fact]
    public void TryMarkHostDisconnected_AlreadyDissolved_PurgesRoom()
    {
        var timeProvider = new FixedTimeProvider(new DateTimeOffset(2026, 7, 23, 12, 0, 0, TimeSpan.Zero));
        var manager = CreateManager(new SequenceRoomCodeGenerator("ABC234"), timeProvider: timeProvider);
        var created = manager.CreateRoom(Guid.NewGuid());
        manager.MarkRoomReady("ABC234", created.Session!.Token);
        manager.MarkRoomForDissolution("ABC234");

        timeProvider.Advance(TimeSpan.FromSeconds(DefaultDissolutionGracePeriodSeconds));

        var marked = manager.TryMarkHostDisconnected("ABC234", created.Session!.DeviceSessionId, "stale");

        marked.ShouldBeFalse();
        manager.AuthenticateSession(created.Session!.Token).ShouldBeNull();
    }

    [Theory]
    [InlineData(RoomState.Created)]
    [InlineData(RoomState.Active)]
    [InlineData(RoomState.Closed)]
    public void AuthenticateSession_RoomInAnyState_ExpiresAfterTwoHours(RoomState state)
    {
        var timeProvider = new FixedTimeProvider(new DateTimeOffset(2026, 7, 23, 12, 0, 0, TimeSpan.Zero));
        var manager = CreateManager(new SequenceRoomCodeGenerator("ABC234"), timeProvider: timeProvider);
        var created = manager.CreateRoom(Guid.NewGuid());
        if (state is RoomState.Active or RoomState.Closed)
        {
            manager.MarkRoomReady("ABC234", created.Session!.Token);
        }
        if (state is RoomState.Closed)
        {
            manager.CloseRoom("ABC234", created.Session!.Token);
        }

        timeProvider.Advance(TimeSpan.FromSeconds(DefaultRoomTtlSeconds));

        manager.AuthenticateSession(created.Session!.Token).ShouldBeNull();
    }

    [Fact]
    public void CreateRoom_WithCustomTtl_ExpiresAtConfiguredDuration()
    {
        var now = new DateTimeOffset(2026, 7, 20, 12, 0, 0, TimeSpan.Zero);
        var manager = CreateManager(
            new SequenceRoomCodeGenerator("ABC234"),
            now: now,
            roomTtlSeconds: 600);

        var result = manager.CreateRoom(Guid.NewGuid());

        result.Room!.ExpiresAt.ShouldBe(now.AddSeconds(600));
        result.Session!.ExpiresAt.ShouldBe(now.AddSeconds(600));
    }

    [Fact]
    public void CancelDissolution_WithinGracePeriod_RoomRemainsAlive()
    {
        var timeProvider = new FixedTimeProvider(new DateTimeOffset(2026, 7, 23, 12, 0, 0, TimeSpan.Zero));
        var manager = CreateManager(new SequenceRoomCodeGenerator("ABC234"), timeProvider: timeProvider);
        var created = manager.CreateRoom(Guid.NewGuid());
        manager.MarkRoomReady("ABC234", created.Session!.Token);
        manager.MarkRoomForDissolution("ABC234");

        timeProvider.Advance(TimeSpan.FromSeconds(DefaultDissolutionGracePeriodSeconds / 2));

        manager.CancelRoomDissolution("ABC234");

        manager.AuthenticateSession(created.Session!.Token).ShouldNotBeNull();
        manager.JoinRoom("ABC234", sessionToken: null).Outcome.ShouldBe(RoomJoinOutcome.Joined);
    }

    [Fact]
    public void CreateRoom_WithCustomTtl_RoomExpiresAfterConfiguredDuration()
    {
        var timeProvider = new FixedTimeProvider(new DateTimeOffset(2026, 7, 20, 12, 0, 0, TimeSpan.Zero));
        var manager = CreateManager(
            new SequenceRoomCodeGenerator("ABC234"),
            timeProvider: timeProvider,
            roomTtlSeconds: 600);
        var created = manager.CreateRoom(Guid.NewGuid());

        timeProvider.Advance(TimeSpan.FromSeconds(600));

        manager.AuthenticateSession(created.Session!.Token).ShouldBeNull();
    }

    [Fact]
    public void Dissolution_WithCustomGracePeriod_UsesConfiguredDuration()
    {
        var timeProvider = new FixedTimeProvider(new DateTimeOffset(2026, 7, 23, 12, 0, 0, TimeSpan.Zero));
        var manager = CreateManager(
            new SequenceRoomCodeGenerator("ABC234"),
            timeProvider: timeProvider,
            dissolutionGracePeriodSeconds: 15);
        var created = manager.CreateRoom(Guid.NewGuid());
        manager.MarkRoomReady("ABC234", created.Session!.Token);
        manager.MarkRoomForDissolution("ABC234");

        // Advance 14 seconds — still within the 15-second grace period; room is joinable.
        timeProvider.Advance(TimeSpan.FromSeconds(14));
        manager.JoinRoom("ABC234", sessionToken: null).Outcome.ShouldBe(RoomJoinOutcome.Joined);

        // Advance 1 more second — now at the 15-second deadline; room is dissolved.
        timeProvider.Advance(TimeSpan.FromSeconds(1));
        manager.JoinRoom("ABC234", sessionToken: null).Outcome.ShouldBe(RoomJoinOutcome.RoomNotFound);
    }

    [Fact]
    public void TryMarkHostDisconnected_RoomNotFound_ReturnsFalse()
    {
        var manager = CreateManager(new SequenceRoomCodeGenerator("ABC234"));

        var result = manager.TryMarkHostDisconnected("NOEXIST", Guid.NewGuid(), "conn-1");

        result.ShouldBeFalse();
    }

    [Fact]
    public void MarkRoomForDissolution_RoomNotFound_DoesNothing()
    {
        var manager = CreateManager(new SequenceRoomCodeGenerator("ABC234"));

        // Should not throw
        manager.MarkRoomForDissolution("NOEXIST");
    }

    [Fact]
    public void CancelRoomDissolution_RoomNotFound_DoesNothing()
    {
        var manager = CreateManager(new SequenceRoomCodeGenerator("ABC234"));

        // Should not throw
        manager.CancelRoomDissolution("NOEXIST");
    }

    [Fact]
    public void RegisterConnection_RoomNotFound_ReturnsNull()
    {
        var manager = CreateManager(new SequenceRoomCodeGenerator("ABC234"));

        var result = manager.RegisterConnection("NOEXIST", Guid.NewGuid(), "conn-1");

        result.ShouldBeNull();
    }

    [Fact]
    public void UnregisterConnection_RoomNotFound_ReturnsFalse()
    {
        var manager = CreateManager(new SequenceRoomCodeGenerator("ABC234"));

        var result = manager.UnregisterConnection("NOEXIST", Guid.NewGuid(), "conn-1");

        result.ShouldBeFalse();
    }

    [Fact]
    public void GetHostConnectionId_RoomNotFound_ReturnsNull()
    {
        var manager = CreateManager(new SequenceRoomCodeGenerator("ABC234"));

        var result = manager.GetHostConnectionId("NOEXIST");

        result.ShouldBeNull();
    }

    [Fact]
    public void GetConnectionId_RoomNotFound_ReturnsNull()
    {
        var manager = CreateManager(new SequenceRoomCodeGenerator("ABC234"));

        var result = manager.GetConnectionId("NOEXIST", Guid.NewGuid());

        result.ShouldBeNull();
    }

    #region Logging

    [Fact]
    public void CreateRoom_LogsInformation_WithRoomCode()
    {
        var logger = new CapturingLogger<RoomManager>();
        var manager = CreateManager(new SequenceRoomCodeGenerator("ABC234"), logger: logger);

        manager.CreateRoom(Guid.NewGuid());

        logger.GetMessages(LogLevel.Information).ShouldContain(
            message => message.Contains("Room ABC234 created", StringComparison.Ordinal));
    }

    [Fact]
    public void CreateRoom_AtCapacity_LogsWarning()
    {
        var logger = new CapturingLogger<RoomManager>();
        var manager = CreateManager(new SequenceRoomCodeGenerator("ABC234"), maxConcurrentRooms: 0, logger: logger);

        manager.CreateRoom(Guid.NewGuid());

        logger.GetMessages(LogLevel.Warning).ShouldContain(
            message => message.Contains("relay capacity reached", StringComparison.Ordinal));
    }

    [Fact]
    public void JoinRoom_RoomNotFound_LogsWarning()
    {
        var logger = new CapturingLogger<RoomManager>();
        var manager = CreateManager(new SequenceRoomCodeGenerator("ABC234"), logger: logger);

        manager.JoinRoom("NOEXIST", sessionToken: null);

        logger.GetMessages(LogLevel.Warning).ShouldContain(
            message => message.Contains("room not found", StringComparison.Ordinal));
    }

    [Fact]
    public void JoinRoom_Joined_LogsInformation_WithMemberCount()
    {
        var logger = new CapturingLogger<RoomManager>();
        var manager = CreateManager(new SequenceRoomCodeGenerator("ABC234"), logger: logger);

        var creation = manager.CreateRoom(Guid.NewGuid());
        manager.MarkRoomReady("ABC234", creation.Session!.Token);
        manager.JoinRoom("ABC234", sessionToken: null);

        logger.GetMessages(LogLevel.Information).ShouldContain(
            message => message.Contains("joined room ABC234", StringComparison.Ordinal)
                && message.Contains("2 member(s)", StringComparison.Ordinal));
    }

    [Fact]
    public void TwoDevices_HostAndJoiner_HaveDistinctDeviceSessions_AndNoPlayerIdentityInHubState()
    {
        var hostGameId = Guid.NewGuid();
        var manager = CreateManager(new SequenceRoomCodeGenerator("ABC234"));

        // Host creates room
        var createResult = manager.CreateRoom(hostGameId);
        createResult.Outcome.ShouldBe(RoomCreationOutcome.Created);
        var room = createResult.Room!;
        var hostSession = createResult.Session!;
        var hostDeviceSessionId = hostSession.DeviceSessionId;

        manager.RegisterConnection("ABC234", hostDeviceSessionId, "conn-host");
        manager.MarkRoomReady("ABC234", hostSession.Token);

        // Joiner device joins room
        var joinResult = manager.JoinRoom("ABC234", sessionToken: null);
        joinResult.Outcome.ShouldBe(RoomJoinOutcome.Joined);
        var joinerSession = joinResult.Session!;
        var joinerDeviceSessionId = joinerSession.DeviceSessionId;

        manager.RegisterConnection("ABC234", joinerDeviceSessionId, "conn-joiner");

        // Assert room has exactly 2 distinct device sessions and 2 connections
        room.Members.Count.ShouldBe(2);
        hostDeviceSessionId.ShouldNotBe(joinerDeviceSessionId);
        hostDeviceSessionId.ShouldNotBe(Guid.Empty);
        joinerDeviceSessionId.ShouldNotBe(Guid.Empty);
        room.LiveConnectionCount.ShouldBe(2);

        room.GetConnectionId(hostDeviceSessionId).ShouldBe("conn-host");
        room.GetConnectionId(joinerDeviceSessionId).ShouldBe("conn-joiner");

        // Assert Hub state exposes device sessions and connections only - no player identity
        var memberTypes = typeof(RoomMember).GetProperties().Select(p => p.Name).ToList();
        memberTypes.ShouldNotContain("PlayerId");
        memberTypes.ShouldNotContain("PlayerName");

        var sessionTypes = typeof(RoomSession).GetProperties().Select(p => p.Name).ToList();
        sessionTypes.ShouldNotContain("PlayerId");
        sessionTypes.ShouldNotContain("PlayerName");

        var roomTypes = typeof(Room).GetProperties().Select(p => p.Name).ToList();
        roomTypes.ShouldNotContain("HostPlayerId");
        roomTypes.ShouldNotContain("PlayerCount");

        // Exactly two device channels exist for the room session
        manager.GetConnectionId("ABC234", hostDeviceSessionId).ShouldBe("conn-host");
        manager.GetConnectionId("ABC234", joinerDeviceSessionId).ShouldBe("conn-joiner");
    }

    #endregion

    private static RoomManager CreateManager(
        IRoomCodeGenerator roomCodeGenerator,
        int maxConcurrentRooms = 10,
        DateTimeOffset? now = null,
        FixedTimeProvider? timeProvider = null,
        int roomTtlSeconds = DefaultRoomTtlSeconds,
        int dissolutionGracePeriodSeconds = DefaultDissolutionGracePeriodSeconds,
        ILogger<RoomManager>? logger = null) =>
        new(
            roomCodeGenerator,
            timeProvider ?? new FixedTimeProvider(now ?? DateTimeOffset.UtcNow),
            Options.Create(new HubOptions
            {
                ApiKey = "test-api-key",
                MaxConcurrentRooms = maxConcurrentRooms,
                RoomTtlSeconds = roomTtlSeconds,
                DissolutionGracePeriodSeconds = dissolutionGracePeriodSeconds
            }),
            logger ?? NullLogger<RoomManager>.Instance);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan offset) => _now += offset;
    }

    private sealed class SequenceRoomCodeGenerator(params string[] roomCodes) : IRoomCodeGenerator
    {
        private readonly Queue<string> _roomCodes = new(roomCodes);

        public int GeneratedCount { get; private set; }

        public string Generate()
        {
            GeneratedCount++;
            return _roomCodes.Dequeue();
        }
    }

    private sealed class AlwaysSameCodeGenerator(string code) : IRoomCodeGenerator
    {
        public int GeneratedCount { get; private set; }

        public string Generate()
        {
            GeneratedCount++;
            return code;
        }
    }
}
