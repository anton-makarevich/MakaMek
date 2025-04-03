using NSubstitute;
using Sanet.MakaMek.Core.Models.Game.Commands;
using Sanet.MakaMek.Core.Models.Game.Commands.Server;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.Transport;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Services.Transport;

public class CommandPublisherTests
{
    private readonly CommandPublisher _publisher;
    private readonly ITransportPublisher _transportPublisher = Substitute.For<ITransportPublisher>();
    private Action<TransportMessage>? _transportCallback; // Capture the callback passed to the *transport* mock

    public CommandPublisherTests()
    {
        // Capture the Subscribe callback given to the mock transport publisher
        _transportPublisher.When(x => x.Subscribe(Arg.Any<Action<TransportMessage>>()))
            .Do(x => _transportCallback = x.Arg<Action<TransportMessage>>());

        // Create a real adapter instance using the mock publisher
        var adapter = new CommandTransportAdapter(_transportPublisher);

        // Create the publisher using the real adapter
        _publisher = new CommandPublisher(adapter); 
        
        // Publisher constructor calls adapter.Initialize, which should call _transportPublisher.Subscribe
        _transportPublisher.Received(1).Subscribe(Arg.Any<Action<TransportMessage>>());
        _transportCallback.ShouldNotBeNull(); // Callback should be captured by now
    }

    [Fact]
    public void PublishCommand_DelegatesTo_AdapterWhichPublishesToTransport()
    {
        // Arrange
        var command = new TurnIncrementedCommand
        {
            GameOriginId = Guid.NewGuid()
        };

        // Act
        _publisher.PublishCommand(command);

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
        
        _publisher.Subscribe(cmd => receivedCommand = cmd);
        
        // Prepare a message as it would come from the transport
        var commandToSend = new TurnIncrementedCommand { GameOriginId = sourceId, Timestamp = timestamp };
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
    }

    [Fact]
    public void Subscribe_MultipleSubscribers_AllReceiveCommands()
    {
        // Arrange
        var sourceId = Guid.NewGuid();
        var timestamp = DateTime.UtcNow;
        var receivedBySubscriber1 = false;
        var receivedBySubscriber2 = false;
        
        _publisher.Subscribe(_ => receivedBySubscriber1 = true);
        _publisher.Subscribe(_ => receivedBySubscriber2 = true);
        
        var commandToSend = new TurnIncrementedCommand { GameOriginId = sourceId, Timestamp = timestamp };
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
        _publisher.Subscribe(_ => throw new Exception("Test exception"));
        
        // Second subscriber should still be called
        _publisher.Subscribe(_ => receivedBySubscriber2 = true);
        
        var commandToSend = new TurnIncrementedCommand { GameOriginId = sourceId, Timestamp = timestamp };
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

    // Tests for UnknownCommandTypeException and JsonException are removed 
    // as they primarily test the adapter's deserialization logic, which is covered 
    // in CommandTransportAdapterTests.cs
}
