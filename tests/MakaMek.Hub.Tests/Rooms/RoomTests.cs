using Sanet.MakaMek.Hub.Rooms;
using Shouldly;

namespace Sanet.MakaMek.Hub.Tests.Rooms;

public class RoomTests
{
    private static readonly DateTimeOffset DefaultNow = new(2026, 7, 20, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromHours(2);

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

    private static Room CreateRoom(Guid hostId)
    {
        var hostMember = new RoomMember(hostId, "Ada", RoomRole.Host, DefaultNow);
        var hostSession = new RoomSession("host-token", "ABC234", hostId, RoomRole.Host, DefaultNow.Add(DefaultTtl));
        return new Room("ABC234", hostMember, hostSession, DefaultNow, DefaultNow.Add(DefaultTtl));
    }
}
