using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MakaMek.Core.Services.Transport.Relay;
using Sanet.Transport;
using Sanet.Transport.Relay.Contracts;
using Sanet.Transport.SignalR.Client.Factories;
using Sanet.Transport.SignalR.Client.Relay;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game;

public class RelayRoomConnectorTests
{
    private const string RoomCode = "ABCDEF";
    private const string SessionToken = "session-token";

    private readonly IRelayRoomClient _roomClient = Substitute.For<IRelayRoomClient>();
    private readonly IPublisherFactory _publisherFactory = Substitute.For<IPublisherFactory>();
    private readonly ICommandTransportAdapter _transportAdapter = Substitute.For<ICommandTransportAdapter>();
    private readonly RelayRoomConnector _sut;

    public RelayRoomConnectorTests()
    {
        _sut = new RelayRoomConnector(
            _roomClient, _publisherFactory, _transportAdapter, NullLogger.Instance);
    }

    [Fact]
    public async Task ConnectAsync_WhenRoomIsIncompatibleAndMembershipRemovalFails_ReturnsIncompatibilityError()
    {
        // Arrange - a hub serves rooms for many games, so a room carrying another title is refused
        // and the guest membership is rolled back. A relay that also fails that rollback must not
        // replace the reason the join was refused.
        var deviceSessionId = Guid.NewGuid();
        _roomClient.Join(RoomCode, sessionToken: null, Arg.Any<CancellationToken>())
            .Returns(RoomSessionResult.Succeeded(
                RoomCode,
                SessionToken,
                "Client",
                deviceSessionId,
                new RoomGameInfo(Guid.NewGuid(), "SomeOtherGame", "1.0.0")));
        _roomClient.RemoveMember(
                RoomCode,
                SessionToken,
                deviceSessionId,
                Arg.Any<CancellationToken>(),
                Arg.Any<RelayClientOptions?>())
            .Returns(Task.FromException<RoomOperationResult>(
                new InvalidOperationException("The relay is unavailable.")));

        // Act
        var result = await _sut.ConnectAsync(
            RoomCode, sessionToken: null, CreateRelayOptions(), CancellationToken.None);

        // Assert
        result.Publisher.ShouldBeNull();
        result.Error.ShouldNotBeNull()
            .Message.ShouldBe("The room was not created by a compatible MakaMek game.");
        await _publisherFactory.DidNotReceive().Create(
            Arg.Any<RelayPublisherOptions>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ConnectAsync_WhenRelayReturnsNoTicketAndNoError_ReturnsUnknownError()
    {
        // Arrange - a relay that refuses to issue a ticket without reporting why must still give
        // the caller an actionable error instead of a successful join without a publisher.
        var deviceSessionId = Guid.NewGuid();
        _roomClient.Join(RoomCode, sessionToken: null, Arg.Any<CancellationToken>())
            .Returns(RoomSessionResult.Succeeded(
                RoomCode,
                SessionToken,
                "Client",
                deviceSessionId,
                RelayGameInfoFactory.Create(Guid.NewGuid())));
        _roomClient.GetRelayTicket(
                RoomCode, SessionToken, Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>())
            .Returns(new RelayTicketResult(false, null, null, null));

        // Act
        var result = await _sut.ConnectAsync(
            RoomCode, sessionToken: null, CreateRelayOptions(), CancellationToken.None);

        // Assert
        result.Publisher.ShouldBeNull();
        var error = result.Error.ShouldNotBeNull();
        error.Code.ShouldBe(RelayClientErrorCode.Unknown);
        error.Message.ShouldBe("The relay did not issue a relay ticket for the joining session.");
        await _roomClient.Received(1).RemoveMember(
            RoomCode,
            SessionToken,
            deviceSessionId,
            Arg.Any<CancellationToken>(),
            Arg.Any<RelayClientOptions?>());
        await _publisherFactory.DidNotReceive().Create(
            Arg.Any<RelayPublisherOptions>(), Arg.Any<CancellationToken>());
    }

    private static RelayClientOptions CreateRelayOptions() => new()
    {
        BaseUrl = "http://hub.local",
        ApiKey = "api-key"
    };
}
