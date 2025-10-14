using NSubstitute;
using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.Transport;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Services.Transport;

public class CommandPublisherTests
{
    private readonly CommandPublisher _sut;
    private readonly ITransportPublisher _transportPublisher = Substitute.For<ITransportPublisher>();
    private Action<TransportMessage>? _transportCallback; // Capture the callback passed to the *transport* mock
    private readonly CommandTransportAdapter _adapter;
    public CommandPublisherTests()
    {
        // Capture the Subscribe callback given to the mock transport publisher
        _transportPublisher.When(x => x.Subscribe(Arg.Any<Action<TransportMessage>>()))
            .Do(x => _transportCallback = x.Arg<Action<TransportMessage>>());

        // Create a real adapter instance using the mock publisher
        
        _adapter = new CommandTransportAdapter(_transportPublisher);

        // Create the publisher using the real adapter
        _sut = new CommandPublisher(_adapter); 
    }

    [Fact]
    public void PublishCommand_DelegatesTo_AdapterWhichPublishesToTransport()
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
        // Verify that the underlying mock transport publisher received the message via the adapter
        _transportPublisher.Received(1).PublishMessage(Arg.Is<TransportMessage>(msg => 
            msg.MessageType == nameof(TurnIncrementedCommand) && 
            msg.SourceId == command.GameOriginId));
    }

    [Fact]
    public void Subscribe_ReceivesCommands_WhenTransportCallbackInvoked()
    {
        // Arrange
        var sourceId = Guid.NewGuid();
        var timestamp = DateTime.UtcNow;
        IGameCommand? receivedCommand = null;
        
        _sut.Subscribe(cmd => receivedCommand = cmd);
        
        // Prepare a message as it would come from the transport
        var commandToSend = new TurnIncrementedCommand
        {
            GameOriginId = sourceId,
            TurnNumber = 1
        };
        var payload = System.Text.Json.JsonSerializer.Serialize(commandToSend);
        var message = new TransportMessage
        {
            MessageType = nameof(TurnIncrementedCommand),
            SourceId = sourceId,
            Timestamp = timestamp,
            Payload = payload
        };

        // Act - simulate receiving the message by invoking the captured transport callback
        _transportCallback!(message);

        // Assert
        receivedCommand.ShouldNotBeNull();
        receivedCommand.ShouldBeOfType<TurnIncrementedCommand>();
        receivedCommand.GameOriginId.ShouldBe(sourceId);
        receivedCommand.Timestamp.ShouldBe(timestamp);
        // Should call _transportPublisher.Subscribe
        _transportPublisher.Received(1).Subscribe(Arg.Any<Action<TransportMessage>>());
        _transportCallback.ShouldNotBeNull(); // Callback should be captured by now
    }
    
    [Fact]
    public void Subscribe_ShouldSubscribeToTransport()
    {
        // Act
        _sut.Subscribe(cmd => _ = cmd);
        
        // Assert
        _transportPublisher.Received(1).Subscribe(Arg.Any<Action<TransportMessage>>());
        _transportCallback.ShouldNotBeNull(); // Callback should be captured by now
    }
    
    [Fact]
    public void Subscribe_ShouldSubscribeToTransport_WithTransportPublisher()
    {
        // Arrange
        var transportPublisher = Substitute.For<ITransportPublisher>();
        
        // Act
        _sut.Subscribe(cmd => _ = cmd, transportPublisher);
        
        // Assert
        _transportPublisher.Received(1).Subscribe(Arg.Any<Action<TransportMessage>>());
        _transportCallback.ShouldNotBeNull(); // Callback should be captured by now
    }

    [Fact]
    public void Subscribe_MultipleSubscribers_AllReceiveCommands()
    {
        // Arrange
        var sourceId = Guid.NewGuid();
        var timestamp = DateTime.UtcNow;
        var receivedBySubscriber1 = false;
        var receivedBySubscriber2 = false;
        
        _sut.Subscribe(_ => receivedBySubscriber1 = true);
        _sut.Subscribe(_ => receivedBySubscriber2 = true);

        var commandToSend = new TurnIncrementedCommand
        {
            GameOriginId = sourceId,
            TurnNumber = 1
        };
        var payload = System.Text.Json.JsonSerializer.Serialize(commandToSend);
        var message = new TransportMessage
        {
            MessageType = nameof(TurnIncrementedCommand),
            SourceId = sourceId,
            Timestamp = timestamp,
            Payload = payload
        };

        // Act
        _transportCallback!(message);

        // Assert
        receivedBySubscriber1.ShouldBeTrue();
        receivedBySubscriber2.ShouldBeTrue();
    }

    [Fact]
    public void Subscribe_ErrorInOneSubscriber_DoesNotAffectOthers()
    {
        // Arrange
        var sourceId = Guid.NewGuid();
        var timestamp = DateTime.UtcNow;
        var receivedBySubscriber2 = false;
        
        // First subscriber throws an exception
        _sut.Subscribe(_ => throw new Exception("Test exception"));
        
        // Second subscriber should still be called
        _sut.Subscribe(_ => receivedBySubscriber2 = true);

        var commandToSend = new TurnIncrementedCommand
        {
            GameOriginId = sourceId,
            Timestamp = timestamp,
            TurnNumber = 1
        };
        var payload = System.Text.Json.JsonSerializer.Serialize(commandToSend);
        var message = new TransportMessage
        {
            MessageType = nameof(TurnIncrementedCommand),
            SourceId = sourceId,
            Timestamp = timestamp,
            Payload = payload
        };

        // Act - Simulate transport callback. This should not throw despite the first subscriber throwing.
        Should.NotThrow(() => _transportCallback!(message));

        // Assert
        receivedBySubscriber2.ShouldBeTrue();
    }

    [Fact]
    public void Adapter_ReturnsCorrectValue()
    {
        _sut.Adapter.ShouldBe(_adapter);
    }
    
    [Fact]
    public void Unsubscribe_RemovesSubscriber()
    {
        // Arrange
        var received = false;
        var subscriber = new Action<IGameCommand>(_ => received = true);
        _sut.Subscribe(subscriber);

        // Act
        _sut.Unsubscribe(subscriber);

        // Send a command to verify the subscriber was removed
        var sourceId = Guid.NewGuid();
        var timestamp = DateTime.UtcNow;
        var commandToSend = new TurnIncrementedCommand
        {
            GameOriginId = sourceId,
            Timestamp = timestamp,
            TurnNumber = 1
        };
        var payload = System.Text.Json.JsonSerializer.Serialize(commandToSend);
        var message = new TransportMessage
        {
            MessageType = nameof(TurnIncrementedCommand),
            SourceId = sourceId,
            Timestamp = timestamp,
            Payload = payload
        };

        // Act - simulate receiving the message
        _transportCallback!(message);

        // Assert
        received.ShouldBeFalse("The unsubscribed subscriber should not have been called");
    }
}
