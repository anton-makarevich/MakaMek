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
