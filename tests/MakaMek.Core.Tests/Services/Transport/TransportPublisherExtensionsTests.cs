using Microsoft.Extensions.Logging;
using NSubstitute;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.Transport;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Services.Transport;

public class TransportPublisherExtensionsTests
{
    private readonly ICommandTransportAdapter _adapter = Substitute.For<ICommandTransportAdapter>();
    private readonly ILogger _logger = Substitute.For<ILogger>();

    [Fact]
    public async Task RemoveAndDisposeAsync_WhenPublisherIsNull_DoesNothing()
    {
        // Arrange
        ITransportPublisher? publisher = null;

        // Act
        await publisher.RemoveAndDisposeAsync(_adapter, _logger);

        // Assert
        _adapter.DidNotReceive().RemovePublisher(Arg.Any<ITransportPublisher>());
        _logger.DidNotReceiveWithAnyArgs().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task RemoveAndDisposeAsync_WhenSuccessful_RemovesAndDisposesPublisher()
    {
        // Arrange
        var publisher = Substitute.For<ITransportPublisher, IAsyncDisposable>();

        // Act
        await publisher.RemoveAndDisposeAsync(_adapter, _logger);

        // Assert
        _adapter.Received(1).RemovePublisher(publisher);
        await ((IAsyncDisposable)publisher).Received(1).DisposeAsync();
    }

    [Fact]
    public async Task RemoveAndDisposeAsync_WhenRemoveThrows_LogsWarningAndStillDisposes()
    {
        // Arrange
        var publisher = Substitute.For<ITransportPublisher>();
        var removeError = new InvalidOperationException("remove boom");
        _adapter.When(a => a.RemovePublisher(publisher)).Throw(removeError);

        // Act
        var act = () => publisher.RemoveAndDisposeAsync(_adapter, _logger);

        // Assert - should not throw
        await act.ShouldNotThrowAsync();
        await publisher.Received(1).DisposeAsync();
        VerifyWarningLogged(removeError);
    }

    [Fact]
    public async Task RemoveAndDisposeAsync_WhenDisposeThrows_LogsWarningAndDoesNotThrow()
    {
        // Arrange
        var publisher = Substitute.For<ITransportPublisher>();
        var disposeError = new InvalidOperationException("dispose boom");
        publisher.When(x => x.DisposeAsync()).Throw(disposeError);

        // Act
        var act = () => publisher.RemoveAndDisposeAsync(_adapter, _logger);

        // Assert - should not throw
        await act.ShouldNotThrowAsync();
        _adapter.Received(1).RemovePublisher(publisher);
        VerifyWarningLogged(disposeError);
    }

    [Fact]
    public async Task RemoveAndDisposeAsync_WhenBothThrow_LogsTwoWarningsAndDoesNotThrow()
    {
        // Arrange
        var publisher = Substitute.For<ITransportPublisher>();
        _adapter.When(a => a.RemovePublisher(publisher))
            .Throw(new InvalidOperationException("remove boom"));
        publisher.When(x => x.DisposeAsync())
            .Throw(new InvalidOperationException("dispose boom"));

        // Act & Assert - should not throw
        await Should.NotThrowAsync(
            () => publisher.RemoveAndDisposeAsync(_adapter, _logger));
        _logger.Received(2).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task RemoveAndDisposeAsync_LogsMessageContainingPublisherTypeName()
    {
        // Arrange
        var publisher = Substitute.For<ITransportPublisher>();
        _adapter.When(a => a.RemovePublisher(publisher))
            .Throw(new InvalidOperationException("remove boom"));

        object? loggedState = null;
        _logger.When(l => l.Log(
                LogLevel.Warning,
                Arg.Any<EventId>(),
                Arg.Any<object>(),
                Arg.Any<Exception?>(),
                Arg.Any<Func<object, Exception?, string>>()))
            .Do(ci => loggedState = ci.ArgAt<object>(2));

        // Act
        await publisher.RemoveAndDisposeAsync(_adapter, _logger);

        // Assert - log message identifies the publisher by its concrete type name
        loggedState.ShouldNotBeNull();
        loggedState.ToString()!.ShouldContain(publisher.GetType().Name);
    }

    private void VerifyWarningLogged(Exception expectedException)
    {
        _logger.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            expectedException,
            Arg.Any<Func<object, Exception?, string>>());
    }
}
