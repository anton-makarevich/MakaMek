using Microsoft.Extensions.Options;
using Sanet.MakaMek.Hub.Configuration;
using Sanet.MakaMek.Hub.Rooms;
using Shouldly;

namespace Sanet.MakaMek.Hub.Tests.Rooms;

public class RoomManagerTests
{
    [Fact]
    public void CreateRoom_CreatesHostRosterEntrySessionAndTwoHourExpiry()
    {
        var now = new DateTimeOffset(2026, 7, 20, 12, 0, 0, TimeSpan.Zero);
        var playerId = Guid.NewGuid();
        var manager = CreateManager(
            new SequenceRoomCodeGenerator("ABC234"),
            now: now);

        var result = manager.CreateRoom("Ada", playerId);

        result.Outcome.ShouldBe(RoomCreationOutcome.Created);
        result.ActiveRoomCount.ShouldBe(1);
        result.Room.ShouldNotBeNull();
        result.Session.ShouldNotBeNull();
        result.Room!.RoomCode.ShouldBe("ABC234");
        result.Room.HostPlayerId.ShouldBe(playerId);
        result.Room.ExpiresAt.ShouldBe(now.AddHours(2));
        result.Room.Members.Count.ShouldBe(1);
        var host = result.Room.Members.Single();
        host.PlayerId.ShouldBe(playerId);
        host.PlayerName.ShouldBe("Ada");
        host.Role.ShouldBe(RoomRole.Host);
        result.Session!.PlayerId.ShouldBe(playerId);
        result.Session.Role.ShouldBe(RoomRole.Host);
        result.Session.RoomCode.ShouldBe("ABC234");
        result.Session.ExpiresAt.ShouldBe(now.AddHours(2));
        string.IsNullOrWhiteSpace(result.Session.Token).ShouldBeFalse();
    }

    [Fact]
    public void CreateRoom_WhenGeneratedCodeCollides_RetriesUntilItFindsAnAvailableCode()
    {
        var generator = new SequenceRoomCodeGenerator("ABC234", "ABC234", "DEF567");
        var manager = CreateManager(generator);

        var first = manager.CreateRoom("Ada", Guid.NewGuid());
        var second = manager.CreateRoom("Grace", Guid.NewGuid());

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

        manager.CreateRoom("Ada", Guid.NewGuid());
        var result = manager.CreateRoom("Grace", Guid.NewGuid());

        result.Outcome.ShouldBe(RoomCreationOutcome.HubAtCapacity);
        result.ActiveRoomCount.ShouldBe(1);
        result.Room.ShouldBeNull();
        result.Session.ShouldBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateRoom_WithInvalidPlayerName_ThrowsArgumentException(string? playerName)
    {
        var manager = CreateManager(new SequenceRoomCodeGenerator("ABC234"));

        Should.Throw<ArgumentException>(() => manager.CreateRoom(playerName!, Guid.NewGuid()));
    }

    [Fact]
    public void CreateRoom_WithEmptyPlayerId_ThrowsArgumentException()
    {
        var manager = CreateManager(new SequenceRoomCodeGenerator("ABC234"));

        Should.Throw<ArgumentException>(() => manager.CreateRoom("Ada", Guid.Empty));
    }

    [Fact]
    public void CreateRoom_AfterExpiredRoomsAreCleanedUp_Succeeds()
    {
        var now = new DateTimeOffset(2026, 7, 20, 12, 0, 0, TimeSpan.Zero);
        var generator = new SequenceRoomCodeGenerator("ABC234", "DEF567");
        var timeProvider = new FixedTimeProvider(now);
        var manager = CreateManager(generator, maxConcurrentRooms: 1, timeProvider: timeProvider);

        manager.CreateRoom("Ada", Guid.NewGuid());

        timeProvider.Advance(TimeSpan.FromHours(2).Add(TimeSpan.FromMinutes(1)));

        var result = manager.CreateRoom("Grace", Guid.NewGuid());

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

        manager.CreateRoom("Ada", Guid.NewGuid());

        var ex = Should.Throw<InvalidOperationException>(
            () => manager.CreateRoom("Grace", Guid.NewGuid()));

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

        var result = manager.JoinRoom("NOEXIST", "Grace", Guid.NewGuid());

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

        var hostId = Guid.NewGuid();
        manager.CreateRoom("Ada", hostId);

        timeProvider.Advance(TimeSpan.FromHours(2).Add(TimeSpan.FromMinutes(1)));

        var result = manager.JoinRoom("ABC234", "Grace", Guid.NewGuid());

        result.Outcome.ShouldBe(RoomJoinOutcome.RoomExpired);
    }

    [Fact]
    public void JoinRoom_NotReady_ReturnsNotReady()
    {
        var manager = CreateManager(new SequenceRoomCodeGenerator("ABC234"));

        manager.CreateRoom("Ada", Guid.NewGuid());

        var result = manager.JoinRoom("ABC234", "Grace", Guid.NewGuid());

        result.Outcome.ShouldBe(RoomJoinOutcome.HostNotReady);
    }

    [Fact]
    public void JoinRoom_WithHostPlayerId_ReturnsHostPlayerIdConflict()
    {
        var now = new DateTimeOffset(2026, 7, 20, 12, 0, 0, TimeSpan.Zero);
        var manager = CreateManager(
            new SequenceRoomCodeGenerator("ABC234"),
            now: now);

        var hostId = Guid.NewGuid();
        var createResult = manager.CreateRoom("Ada", hostId);
        manager.MarkRoomReady("ABC234", createResult.Session!.Token);

        var result = manager.JoinRoom("ABC234", "Malicious", hostId);

        result.Outcome.ShouldBe(RoomJoinOutcome.HostPlayerIdConflict);
    }

    [Fact]
    public void JoinRoom_ReadyRoom_AppendsMemberAndIssuesSession()
    {
        var now = new DateTimeOffset(2026, 7, 20, 12, 0, 0, TimeSpan.Zero);
        var manager = CreateManager(
            new SequenceRoomCodeGenerator("ABC234"),
            now: now);

        var hostId = Guid.NewGuid();
        var createResult = manager.CreateRoom("Ada", hostId);
        manager.MarkRoomReady("ABC234", createResult.Session!.Token);

        var playerId = Guid.NewGuid();
        var result = manager.JoinRoom("ABC234", "Grace", playerId);

        result.Outcome.ShouldBe(RoomJoinOutcome.Joined);
        result.Room.ShouldNotBeNull();
        result.Session.ShouldNotBeNull();
        result.Room!.RoomCode.ShouldBe("ABC234");
        result.Room.Members.Count.ShouldBe(2);
        result.Session!.PlayerId.ShouldBe(playerId);
        result.Session.Role.ShouldBe(RoomRole.Client);
        result.Session.RoomCode.ShouldBe("ABC234");
        result.Session.ExpiresAt.ShouldBe(now.AddHours(2));
        string.IsNullOrWhiteSpace(result.Session.Token).ShouldBeFalse();
    }

    [Fact]
    public void JoinRoom_DuplicatePlayerId_ReplacesExistingEntry()
    {
        var now = new DateTimeOffset(2026, 7, 20, 12, 0, 0, TimeSpan.Zero);
        var manager = CreateManager(
            new SequenceRoomCodeGenerator("ABC234"),
            now: now);

        var hostId = Guid.NewGuid();
        var createResult = manager.CreateRoom("Ada", hostId);
        manager.MarkRoomReady("ABC234", createResult.Session!.Token);

        var playerId = Guid.NewGuid();
        manager.JoinRoom("ABC234", "Grace", playerId);
        var result = manager.JoinRoom("ABC234", "Grace v2", playerId);

        result.Outcome.ShouldBe(RoomJoinOutcome.Joined);
        result.Room!.Members.Count.ShouldBe(2);
    }

    [Fact]
    public void JoinRoom_SessionExpiresAtRoomExpiry()
    {
        var now = new DateTimeOffset(2026, 7, 20, 12, 0, 0, TimeSpan.Zero);
        var manager = CreateManager(
            new SequenceRoomCodeGenerator("ABC234"),
            now: now);

        var hostId = Guid.NewGuid();
        var createResult = manager.CreateRoom("Ada", hostId);
        manager.MarkRoomReady("ABC234", createResult.Session!.Token);

        var result = manager.JoinRoom("ABC234", "Grace", Guid.NewGuid());

        result.Session!.ExpiresAt.ShouldBe(result.Room!.ExpiresAt);
    }

    [Fact]
    public void MarkRoomReady_HostMarksReady_Succeeds()
    {
        var now = new DateTimeOffset(2026, 7, 20, 12, 0, 0, TimeSpan.Zero);
        var manager = CreateManager(
            new SequenceRoomCodeGenerator("ABC234"),
            now: now);

        var hostId = Guid.NewGuid();
        var createResult = manager.CreateRoom("Ada", hostId);

        var result = manager.MarkRoomReady("ABC234", createResult.Session!.Token);

        result.Outcome.ShouldBe(RoomReadyOutcome.Ready);
    }

    [Fact]
    public void MarkRoomReady_NonHost_ReturnsNotHost()
    {
        var manager = CreateManager(new SequenceRoomCodeGenerator("ABC234"));

        var hostId = Guid.NewGuid();
        manager.CreateRoom("Ada", hostId);

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

        var hostId = Guid.NewGuid();
        var createResult = manager.CreateRoom("Ada", hostId);

        timeProvider.Advance(TimeSpan.FromHours(2).Add(TimeSpan.FromMinutes(1)));

        var result = manager.MarkRoomReady("ABC234", createResult.Session!.Token);

        result.Outcome.ShouldBe(RoomReadyOutcome.RoomExpired);
    }

    [Fact]
    public void JoinRoom_WithInvalidPlayerName_ThrowsArgumentException()
    {
        var manager = CreateManager(new SequenceRoomCodeGenerator("ABC234"));

        Should.Throw<ArgumentException>(() => manager.JoinRoom("ABC234", "", Guid.NewGuid()));
    }

    [Fact]
    public void JoinRoom_WithEmptyPlayerId_ThrowsArgumentException()
    {
        var manager = CreateManager(new SequenceRoomCodeGenerator("ABC234"));

        Should.Throw<ArgumentException>(() => manager.JoinRoom("ABC234", "Grace", Guid.Empty));
    }

    [Fact]
    public void MarkRoomReady_WhenRoomAlreadyActive_ReturnsInvalidRoomState()
    {
        var manager = CreateManager(new SequenceRoomCodeGenerator("ABC234"));
        var createResult = manager.CreateRoom("Ada", Guid.NewGuid());
        manager.MarkRoomReady("ABC234", createResult.Session!.Token);

        var result = manager.MarkRoomReady("ABC234", createResult.Session.Token);

        result.Outcome.ShouldBe(RoomReadyOutcome.InvalidRoomState);
        createResult.Room!.State.ShouldBe(RoomState.Active);
    }

    [Fact]
    public void CloseRoom_ActiveRoom_TransitionsToClosed()
    {
        var manager = CreateManager(new SequenceRoomCodeGenerator("ABC234"));
        var createResult = manager.CreateRoom("Ada", Guid.NewGuid());
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

        var createResult = manager.CreateRoom("Ada", Guid.NewGuid());
        manager.MarkRoomReady("ABC234", createResult.Session!.Token);

        timeProvider.Advance(TimeSpan.FromHours(2).Add(TimeSpan.FromMinutes(1)));

        var result = manager.CloseRoom("ABC234", createResult.Session.Token);

        result.Outcome.ShouldBe(RoomCloseOutcome.RoomExpired);
    }

    [Fact]
    public void CloseRoom_NonHost_ReturnsNotHost()
    {
        var manager = CreateManager(new SequenceRoomCodeGenerator("ABC234"));
        var createResult = manager.CreateRoom("Ada", Guid.NewGuid());
        manager.MarkRoomReady("ABC234", createResult.Session!.Token);

        var result = manager.CloseRoom("ABC234", "not-the-host-token");

        result.Outcome.ShouldBe(RoomCloseOutcome.NotHost);
        createResult.Room!.State.ShouldBe(RoomState.Active);
    }

    [Fact]
    public void CloseRoom_WhenRoomNotActive_ReturnsInvalidRoomState()
    {
        var manager = CreateManager(new SequenceRoomCodeGenerator("ABC234"));
        var createResult = manager.CreateRoom("Ada", Guid.NewGuid());

        var result = manager.CloseRoom("ABC234", createResult.Session!.Token);

        result.Outcome.ShouldBe(RoomCloseOutcome.InvalidRoomState);
        createResult.Room!.State.ShouldBe(RoomState.Created);
    }

    [Fact]
    public void JoinRoom_ClosedRoom_UnknownPlayer_ReturnsRoomFull()
    {
        var manager = CreateManager(new SequenceRoomCodeGenerator("ABC234"));
        var createResult = manager.CreateRoom("Ada", Guid.NewGuid());
        manager.MarkRoomReady("ABC234", createResult.Session!.Token);
        manager.CloseRoom("ABC234", createResult.Session.Token);

        var result = manager.JoinRoom("ABC234", "Grace", Guid.NewGuid());

        result.Outcome.ShouldBe(RoomJoinOutcome.RoomFull);
        createResult.Room!.State.ShouldBe(RoomState.Closed);
        createResult.Room.Members.Count.ShouldBe(1);
    }

    [Fact]
    public void JoinRoom_ClosedRoom_RosteredPlayer_ReturnsJoined()
    {
        var manager = CreateManager(new SequenceRoomCodeGenerator("ABC234"));
        var createResult = manager.CreateRoom("Ada", Guid.NewGuid());
        manager.MarkRoomReady("ABC234", createResult.Session!.Token);

        var playerId = Guid.NewGuid();
        manager.JoinRoom("ABC234", "Grace", playerId);
        manager.CloseRoom("ABC234", createResult.Session!.Token);

        var result = manager.JoinRoom("ABC234", "Grace", playerId);

        result.Outcome.ShouldBe(RoomJoinOutcome.Joined);
        result.Session.ShouldNotBeNull();
        result.Session!.PlayerId.ShouldBe(playerId);
        createResult.Room!.State.ShouldBe(RoomState.Closed);
    }

    [Fact]
    public void RemoveMember_RemovesRosterEntryAndRevokesSessions()
    {
        var manager = CreateManager(new SequenceRoomCodeGenerator("ABC234"));
        var createResult = manager.CreateRoom("Ada", Guid.NewGuid());
        manager.MarkRoomReady("ABC234", createResult.Session!.Token);

        var playerId = Guid.NewGuid();
        var joinResult = manager.JoinRoom("ABC234", "Grace", playerId);
        var clientToken = joinResult.Session!.Token;

        createResult.Room!.HasSession(clientToken).ShouldBeTrue();

        var result = manager.RemoveMember("ABC234", createResult.Session!.Token, playerId);

        result.Outcome.ShouldBe(RoomRemoveMemberOutcome.Removed);
        createResult.Room.IsMember(playerId).ShouldBeFalse();
        createResult.Room.HasSession(clientToken).ShouldBeFalse();
        createResult.Room.Members.Count.ShouldBe(1);
    }

    [Fact]
    public void RemoveMember_UnknownPlayer_ReturnsMemberNotFound()
    {
        var manager = CreateManager(new SequenceRoomCodeGenerator("ABC234"));
        var createResult = manager.CreateRoom("Ada", Guid.NewGuid());
        manager.MarkRoomReady("ABC234", createResult.Session!.Token);

        var result = manager.RemoveMember("ABC234", createResult.Session!.Token, Guid.NewGuid());

        result.Outcome.ShouldBe(RoomRemoveMemberOutcome.MemberNotFound);
        createResult.Room!.Members.Count.ShouldBe(1);
    }

    [Fact]
    public void RemoveMember_HostPlayerId_ReturnsCannotRemoveHost()
    {
        var hostId = Guid.NewGuid();
        var manager = CreateManager(new SequenceRoomCodeGenerator("ABC234"));
        var createResult = manager.CreateRoom("Ada", hostId);
        manager.MarkRoomReady("ABC234", createResult.Session!.Token);

        var result = manager.RemoveMember("ABC234", createResult.Session!.Token, hostId);

        result.Outcome.ShouldBe(RoomRemoveMemberOutcome.CannotRemoveHost);
        createResult.Room!.IsMember(hostId).ShouldBeTrue();
    }

    [Fact]
    public void RemoveMember_NonHost_ReturnsNotHost()
    {
        var manager = CreateManager(new SequenceRoomCodeGenerator("ABC234"));
        var createResult = manager.CreateRoom("Ada", Guid.NewGuid());
        manager.MarkRoomReady("ABC234", createResult.Session!.Token);

        var playerId = Guid.NewGuid();
        manager.JoinRoom("ABC234", "Grace", playerId);

        var result = manager.RemoveMember("ABC234", "not-the-host-token", playerId);

        result.Outcome.ShouldBe(RoomRemoveMemberOutcome.NotHost);
        createResult.Room!.IsMember(playerId).ShouldBeTrue();
    }

    private static RoomManager CreateManager(
        IRoomCodeGenerator roomCodeGenerator,
        int maxConcurrentRooms = 10,
        DateTimeOffset? now = null,
        FixedTimeProvider? timeProvider = null) =>
        new(
            roomCodeGenerator,
            timeProvider ?? new FixedTimeProvider(now ?? DateTimeOffset.UtcNow),
            Options.Create(new HubOptions
            {
                ApiKey = "test-api-key",
                MaxConcurrentRooms = maxConcurrentRooms
            }));

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
