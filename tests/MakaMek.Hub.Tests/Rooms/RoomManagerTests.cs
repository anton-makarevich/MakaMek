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
        DateTimeOffset? now = null) =>
        new(
            roomCodeGenerator,
            new FixedTimeProvider(now ?? DateTimeOffset.UtcNow),
            Options.Create(new HubOptions
            {
                ApiKey = "test-api-key",
                MaxConcurrentRooms = maxConcurrentRooms
            }));

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
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
}
