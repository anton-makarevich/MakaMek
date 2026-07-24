using Sanet.MakaMek.Hub.Rooms;
using Shouldly;

namespace Sanet.MakaMek.Hub.Tests.Rooms;

public class RoomTests
{
    private static readonly DateTimeOffset DefaultNow = new(2026, 7, 20, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromHours(2);

    [Fact]
    public void Constructor_SetsExpiresAtFromProvidedValue()
    {
        var hostId = Guid.NewGuid();
        var expiresAt = DefaultNow.Add(DefaultTtl);

        var room = CreateRoom(hostId);

        room.ExpiresAt.ShouldBe(expiresAt);
    }

    [Fact]
    public void RemoveMember_HostPlayerId_ReturnsFalse()
    {
        var hostId = Guid.NewGuid();
        var room = CreateRoom(hostId);

        var result = room.RemoveMember(hostId);

        result.ShouldBeFalse();
        room.IsMember(hostId).ShouldBeTrue();
        room.Members.Count.ShouldBe(1);
    }

    [Fact]
    public void RemoveMember_NonMember_ReturnsFalse()
    {
        var hostId = Guid.NewGuid();
        var room = CreateRoom(hostId);

        var result = room.RemoveMember(Guid.NewGuid());

        result.ShouldBeFalse();
        room.Members.Count.ShouldBe(1);
    }

    [Fact]
    public void RemoveMember_ClientMember_RemovesMemberAndRevokesSessions()
    {
        var hostId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var room = CreateRoom(hostId);
        var clientSession1 = room.AddClientMember("Grace", clientId, DefaultNow, DefaultTtl, () => "client-token-1");
        var clientSession2 = room.AddClientMember("Grace", clientId, DefaultNow, DefaultTtl, () => "client-token-2");

        var result = room.RemoveMember(clientId);

        result.ShouldBeTrue();
        room.IsMember(clientId).ShouldBeFalse();
        room.HasSession(clientSession1.Token).ShouldBeFalse();
        room.HasSession(clientSession2.Token).ShouldBeFalse();
        room.Members.Count.ShouldBe(1);
        room.IsMember(hostId).ShouldBeTrue();
    }

    [Fact]
    public void TryGetSession_WithMismatchedRoomCodeInSession_ReturnsSessionWithDifferentCode()
    {
        var hostId = Guid.NewGuid();
        var hostMember = new RoomMember(hostId, "Ada", RoomRole.Host, DefaultNow);
        var hostSession = new RoomSession("host-token", "WRONG", hostId, RoomRole.Host, DefaultNow.Add(DefaultTtl));
        var room = new Room("ABC234", hostMember, hostSession, DefaultNow, DefaultNow.Add(DefaultTtl));

        var found = room.TryGetSession("host-token", out var session);

        found.ShouldBeTrue();
        session.RoomCode.ShouldBe("WRONG");
        session.RoomCode.ShouldNotBe(room.RoomCode);
    }

    [Fact]
    public void RegisterConnection_ForConnectedPlayer_ReturnsReplacedConnectionAndTouchesRoom()
    {
        var playerId = Guid.NewGuid();
        var room = CreateRoom(Guid.NewGuid());

        room.RegisterConnection(playerId, "old", DefaultNow, DefaultTtl).ShouldBeNull();
        var replaced = room.RegisterConnection(playerId, "new", DefaultNow.AddMinutes(5), DefaultTtl);

        replaced.ShouldBe("old");
        room.GetConnectionId(playerId).ShouldBe("new");
        room.LiveConnectionCount.ShouldBe(1);
        room.LastActivityAt.ShouldBe(DefaultNow.AddMinutes(5));
        room.ExpiresAt.ShouldBe(DefaultNow.AddHours(2).AddMinutes(5));
    }

    [Fact]
    public void RemoveConnection_OnlyRemovesActiveConnection()
    {
        var playerId = Guid.NewGuid();
        var room = CreateRoom(Guid.NewGuid());
        room.RegisterConnection(playerId, "new", DefaultNow, DefaultTtl);

        room.RemoveConnection(playerId, "old", DefaultNow.AddMinutes(1), DefaultTtl).ShouldBeFalse();
        room.GetConnectionId(playerId).ShouldBe("new");
        room.RemoveConnection(playerId, "new", DefaultNow.AddMinutes(2), DefaultTtl).ShouldBeTrue();

        room.GetConnectionId(playerId).ShouldBeNull();
        room.LiveConnectionCount.ShouldBe(0);
        room.LastActivityAt.ShouldBe(DefaultNow.AddMinutes(2));
    }

    [Fact]
    public void Dissolution_CanBeMarkedCancelledAndDetectedAtDeadline()
    {
        var room = CreateRoom(Guid.NewGuid());
        var grace = TimeSpan.FromSeconds(30);

        room.MarkForDissolution(DefaultNow, grace);

        room.IsDissolving.ShouldBeTrue();
        room.IsDissolvedAt(DefaultNow.AddSeconds(29)).ShouldBeFalse();
        room.IsDissolvedAt(DefaultNow.AddSeconds(30)).ShouldBeTrue();
        room.State.ShouldBe(RoomState.Created);

        room.CancelDissolution();
        room.IsDissolving.ShouldBeFalse();
        room.IsDissolvedAt(DefaultNow.AddMinutes(1)).ShouldBeFalse();
    }

    [Fact]
    public void RevokeAllSessions_RevokesHostAndClientSessions()
    {
        var room = CreateRoom(Guid.NewGuid());
        var client = room.AddClientMember("Grace", Guid.NewGuid(), DefaultNow, DefaultTtl, () => "client-token");

        room.RevokeAllSessions();

        room.HasSession("host-token").ShouldBeFalse();
        room.HasSession(client.Token).ShouldBeFalse();
    }

    private static Room CreateRoom(Guid hostId)
    {
        var hostMember = new RoomMember(hostId, "Ada", RoomRole.Host, DefaultNow);
        var hostSession = new RoomSession("host-token", "ABC234", hostId, RoomRole.Host, DefaultNow.Add(DefaultTtl));
        return new Room("ABC234", hostMember, hostSession, DefaultNow, DefaultNow.Add(DefaultTtl));
    }
}
