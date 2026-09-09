using System.Reactive.Concurrency;
using System.Reactive.Subjects;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MakaMek.Presentation.ViewModels;
using Shouldly;

namespace Sanet.MakaMek.Presentation.Tests.ViewModels;

public class ConnectionStatusViewModelTests
{
    [Fact]
    public void Constructor_NullSource_StatusStaysNotConnected()
    {
        // Act
        var sut = new ConnectionStatusViewModel(null, Scheduler.Immediate);

        // Assert
        sut.OnlineConnectionStatus.ShouldBe(ConnectionStatus.NotConnected);
        sut.IsConnectionDegraded.ShouldBeFalse();
    }

    [Fact]
    public void Constructor_SubscribesToInitialValue()
    {
        // Arrange
        var subject = new BehaviorSubject<ConnectionStatus>(ConnectionStatus.Connected);

        // Act
        var sut = new ConnectionStatusViewModel(subject, Scheduler.Immediate);

        // Assert
        sut.OnlineConnectionStatus.ShouldBe(ConnectionStatus.Connected);
        sut.IsConnectionDegraded.ShouldBeFalse();
    }

    [Fact]
    public void StatusUpdates_WhenSourceEmitsValues()
    {
        // Arrange
        var subject = new BehaviorSubject<ConnectionStatus>(ConnectionStatus.Connected);
        var sut = new ConnectionStatusViewModel(subject, Scheduler.Immediate);

        // Act
        subject.OnNext(ConnectionStatus.Reconnecting);

        // Assert
        sut.OnlineConnectionStatus.ShouldBe(ConnectionStatus.Reconnecting);
    }

    [Fact]
    public void IsConnectionDegraded_TrueForReconnectingDisconnectedClosed()
    {
        // Arrange
        var subject = new BehaviorSubject<ConnectionStatus>(ConnectionStatus.Connected);
        var sut = new ConnectionStatusViewModel(subject, Scheduler.Immediate);

        // Act & Assert - Reconnecting
        subject.OnNext(ConnectionStatus.Reconnecting);
        sut.IsConnectionDegraded.ShouldBeTrue();

        // Act & Assert - Disconnected
        subject.OnNext(ConnectionStatus.Disconnected);
        sut.IsConnectionDegraded.ShouldBeTrue();

        // Act & Assert - Closed
        subject.OnNext(ConnectionStatus.Closed);
        sut.IsConnectionDegraded.ShouldBeTrue();
    }

    [Fact]
    public void IsConnectionDegraded_FalseForNotConnectedConnectingConnected()
    {
        // Arrange
        var subject = new BehaviorSubject<ConnectionStatus>(ConnectionStatus.NotConnected);
        var sut = new ConnectionStatusViewModel(subject, Scheduler.Immediate);

        // Act & Assert - NotConnected
        sut.IsConnectionDegraded.ShouldBeFalse();

        // Act & Assert - Connecting
        subject.OnNext(ConnectionStatus.Connecting);
        sut.IsConnectionDegraded.ShouldBeFalse();

        // Act & Assert - Connected
        subject.OnNext(ConnectionStatus.Connected);
        sut.IsConnectionDegraded.ShouldBeFalse();
    }

    [Fact]
    public void Dispose_UnsubscribesFromSource()
    {
        // Arrange
        var subject = new BehaviorSubject<ConnectionStatus>(ConnectionStatus.Connected);
        var sut = new ConnectionStatusViewModel(subject, Scheduler.Immediate);

        // Act
        sut.Dispose();

        // Assert - status changes after disposal are ignored
        subject.OnNext(ConnectionStatus.Disconnected);
        sut.OnlineConnectionStatus.ShouldBe(ConnectionStatus.Connected);
        sut.IsConnectionDegraded.ShouldBeFalse();
    }
}
