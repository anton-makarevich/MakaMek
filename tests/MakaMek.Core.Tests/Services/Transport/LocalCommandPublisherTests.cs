using Microsoft.Extensions.Logging;
using NSubstitute;
using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.Transport;
using Shouldly;
using System.Text.Json;

namespace Sanet.MakaMek.Core.Tests.Services.Transport;

public class LocalCommandPublisherTests
{
    private readonly LocalCommandPublisher _sut;
    private readonly CommandPublisher _shared;
    private readonly ITransportPublisher _rxPublisher;
    private readonly ITransportPublisher _otherPublisher;
    private readonly CommandTransportAdapter _adapter;
    private readonly ILoggerFactory _loggerFactory = Substitute.For<ILoggerFactory>();
    private readonly ILogger<CommandPublisher> _logger = Substitute.For<ILogger<CommandPublisher>>();
    private Action<TransportMessage>? _rxCallback;
    private Action<TransportMessage>? _otherCallback;

    public LocalCommandPublisherTests()
    {
        _rxPublisher = Substitute.For<ITransportPublisher>();
        _otherPublisher = Substitute.For<ITransportPublisher>();

        _rxPublisher.When(x => x.Subscribe(Arg.Any<Action<TransportMessage>>()))
            .Do(x => _rxCallback = x.Arg<Action<TransportMessage>>());
        _otherPublisher.When(x => x.Subscribe(Arg.Any<Action<TransportMessage>>()))
            .Do(x => _otherCallback = x.Arg<Action<TransportMessage>>());

        _loggerFactory.CreateLogger<CommandPublisher>().Returns(_logger);

        _adapter = new CommandTransportAdapter(_loggerFactory, _rxPublisher, _otherPublisher);
        _shared = new CommandPublisher(_adapter, _loggerFactory);
        _sut = new LocalCommandPublisher(_shared, _rxPublisher);
    }

    [Fact]
    public void PublishCommand_SendsOnlyToRxPublisher()
    {
        // Arrange
        var command = new TurnIncrementedCommand
        {
            GameOriginId = Guid.NewGuid(),
            TurnNumber = 1
        };

        // Act
        _sut.PublishCommand(command);

        // Assert
        _rxPublisher.Received(1).PublishMessage(Arg.Any<TransportMessage>());
        _otherPublisher.DidNotReceive().PublishMessage(Arg.Any<TransportMessage>());
    }

    [Fact]
    public void Subscribe_ReceivesOnlyFromRxPublisher()
    {
        // Arrange
        var handlerCallCount = 0;
        _sut.Subscribe(cmd => handlerCallCount++);

        var command = new TurnIncrementedCommand
        {
            GameOriginId = Guid.NewGuid(),
            TurnNumber = 1
        };
        var payload = JsonSerializer.Serialize(command);
        var rxMessage = new TransportMessage
        {
            MessageType = nameof(TurnIncrementedCommand),
            SourceId = command.GameOriginId,
            Timestamp = DateTime.UtcNow,
            Payload = payload
        };
        var otherMessage = new TransportMessage
        {
            MessageType = nameof(TurnIncrementedCommand),
            SourceId = command.GameOriginId,
            Timestamp = DateTime.UtcNow,
            Payload = payload
        };

        // Act - simulate receiving from rx publisher first
        _rxCallback!(rxMessage);

        // Assert - should have received exactly one command
        handlerCallCount.ShouldBe(1);

        // Act - simulate receiving from other publisher
        _otherCallback!(otherMessage);

        // Assert - count remains one: the other transport does not invoke the handler
        handlerCallCount.ShouldBe(1);
    }

    [Fact]
    public void Unsubscribe_StopsReceivingCommands()
    {
        // Arrange
        var received = false;
        Action<IGameCommand> handler = _ => received = true;
        _sut.Subscribe(handler);

        _sut.Unsubscribe(handler);

        var command = new TurnIncrementedCommand
        {
            GameOriginId = Guid.NewGuid(),
            TurnNumber = 1
        };
        var payload = JsonSerializer.Serialize(command);
        var message = new TransportMessage
        {
            MessageType = nameof(TurnIncrementedCommand),
            SourceId = command.GameOriginId,
            Timestamp = DateTime.UtcNow,
            Payload = payload
        };

        // Act
        _rxCallback!(message);

        // Assert
        received.ShouldBeFalse();
    }

    [Fact]
    public void Adapter_ReturnsSharedAdapter()
    {
        _sut.Adapter.ShouldBe(_shared.Adapter);
    }
}
