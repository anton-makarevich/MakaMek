using System.Reactive.Subjects;
using NSubstitute;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Services.Transport;
using Shouldly;
using Xunit;

namespace Sanet.MakaMek.Core.Tests.Models.Game;

public class OnlineStatusForwarderTests
{
    private readonly OnlineStatusForwarder _sut = new();

    private readonly ICommandTransportAdapter _adapter = Substitute.For<ICommandTransportAdapter>();
    private readonly BehaviorSubject<ConnectionStatus> _connectionStatusSubject = new(ConnectionStatus.Connected);

    public OnlineStatusForwarderTests()
    {
        _adapter.ConnectionStatusChanges.Returns(_connectionStatusSubject);
    }

    [Fact]
    public void OnlineConnectionStatus_ReplaysConnectedOnSubscribe()
    {
        // Act
        var statuses = new List<ConnectionStatus>();
        _sut.OnlineConnectionStatus.Subscribe(statuses.Add);

        // Assert - BehaviorSubject replays the initial Connected on subscribe
        statuses.ShouldBe([ConnectionStatus.Connected]);
    }

    [Fact]
    public void Start_ForwardsAdapterStatusToOnlineConnectionStatus()
    {
        // Arrange
        var statuses = new List<ConnectionStatus>();
        _sut.OnlineConnectionStatus.Subscribe(statuses.Add);

        // Act
        _sut.Start(_adapter);
        _connectionStatusSubject.OnNext(ConnectionStatus.Connecting);
        _connectionStatusSubject.OnNext(ConnectionStatus.Reconnecting);
        _connectionStatusSubject.OnNext(ConnectionStatus.Disconnected);
        _connectionStatusSubject.OnNext(ConnectionStatus.Closed);
        _connectionStatusSubject.OnNext(ConnectionStatus.Connected);

        // Assert - the adapter's BehaviorSubject replays its current Connected on Start,
        // joined by the forwarder's own initial replay on subscribe
        statuses.ShouldBe([
            ConnectionStatus.Connected,
            ConnectionStatus.Connected,
            ConnectionStatus.Connecting,
            ConnectionStatus.Reconnecting,
            ConnectionStatus.Disconnected,
            ConnectionStatus.Closed,
            ConnectionStatus.Connected
        ]);
    }

    [Fact]
    public void Start_WhenCalledTwice_StopsListeningToFirstAdapter()
    {
        // Arrange - a second adapter takes over forwarding
        var secondAdapter = Substitute.For<ICommandTransportAdapter>();
        var secondSubject = new BehaviorSubject<ConnectionStatus>(ConnectionStatus.Connected);
        secondAdapter.ConnectionStatusChanges.Returns(secondSubject);
        _sut.Start(_adapter);

        // Act - re-start with a different adapter, then a stale status arrives on the old one
        _sut.Start(secondAdapter);
        secondSubject.OnNext(ConnectionStatus.Reconnecting);
        _connectionStatusSubject.OnNext(ConnectionStatus.Closed);

        // Assert - only the second adapter's stream is forwarded; the old subscription is gone
        var statuses = new List<ConnectionStatus>();
        _sut.OnlineConnectionStatus.Subscribe(statuses.Add);
        statuses.ShouldBe([ConnectionStatus.Reconnecting]);
    }

    [Fact]
    public void Reset_StopsForwardingAndPushesConnected()
    {
        // Arrange
        var statuses = new List<ConnectionStatus>();
        _sut.OnlineConnectionStatus.Subscribe(statuses.Add);
        _sut.Start(_adapter);
        _connectionStatusSubject.OnNext(ConnectionStatus.Disconnected);
        statuses.ShouldContain(ConnectionStatus.Disconnected);

        // Act
        _sut.Reset();

        // Assert - the stream restarts at Connected and no longer follows the adapter
        _connectionStatusSubject.OnNext(ConnectionStatus.Closed);
        statuses.Last().ShouldBe(ConnectionStatus.Connected);
        statuses.ShouldNotContain(ConnectionStatus.Closed);
    }
}