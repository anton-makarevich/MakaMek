using NSubstitute;
using Sanet.MakaMek.Core.Exceptions;
using Sanet.MakaMek.Core.Models.Game.Commands;
using Sanet.MakaMek.Core.Models.Game.Commands.Client;
using Sanet.MakaMek.Core.Models.Game.Commands.Server;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.Transport;
using Shouldly;
using System.Text.Json;

namespace Sanet.MakaMek.Core.Tests.Services.Transport;

public class CommandTransportAdapterTests
{
    private ITransportPublisher _mockPublisher1 = null!;
    private ITransportPublisher _mockPublisher2 = null!;
    private CommandTransportAdapter _adapter = null!;
    private List<ITransportPublisher> _publishers = null!;

    // Helper to set up adapter with a variable number of publishers
    private void SetupAdapter(int publisherCount = 1)
    {
        _publishers = [];
        if (publisherCount >= 1)
        {
            _mockPublisher1 = Substitute.For<ITransportPublisher>();
            _publishers.Add(_mockPublisher1);
        }
        if (publisherCount >= 2)
        {
            _mockPublisher2 = Substitute.For<ITransportPublisher>();
            _publishers.Add(_mockPublisher2);
        }
        
        _adapter = new CommandTransportAdapter(_publishers.ToArray());
    }
    
    [Fact]
    public void PublishCommand_SendsToAllPublishers()
    {
        // Arrange
        SetupAdapter(2); // Use two publishers
        var command = new TurnIncrementedCommand
        {
            GameOriginId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow
        };
        
        TransportMessage? capturedMessage1 = null;
        TransportMessage? capturedMessage2 = null;
        _mockPublisher1.When(x => x.PublishMessage(Arg.Any<TransportMessage>()))
            .Do(x => capturedMessage1 = x.Arg<TransportMessage>());
        _mockPublisher2.When(x => x.PublishMessage(Arg.Any<TransportMessage>()))
            .Do(x => capturedMessage2 = x.Arg<TransportMessage>());

        // Act
        _adapter.PublishCommand(command);

        // Assert
        _mockPublisher1.Received(1).PublishMessage(Arg.Any<TransportMessage>());
        _mockPublisher2.Received(1).PublishMessage(Arg.Any<TransportMessage>());
        
        capturedMessage1.ShouldNotBeNull();
        capturedMessage1!.MessageType.ShouldBe(nameof(TurnIncrementedCommand));
        capturedMessage1.SourceId.ShouldBe(command.GameOriginId);
        capturedMessage1.Timestamp.ShouldBe(command.Timestamp);
        capturedMessage1.Payload.ShouldNotBeNullOrEmpty();
        // Assuming payload contains serialized command
        var deserializedPayload1 = JsonSerializer.Deserialize<TurnIncrementedCommand>(capturedMessage1.Payload);
        deserializedPayload1.Timestamp.ShouldBe(command.Timestamp);
        deserializedPayload1.GameOriginId.ShouldBe(command.GameOriginId);
        // Note: GameOriginId and Timestamp are not serialized in payload, they are part of the TransportMessage

        capturedMessage2.ShouldNotBeNull();
        capturedMessage2.ShouldBeEquivalentTo(capturedMessage1); // Messages should be identical
    }
    
    [Fact]
    public void AddPublisher_AddsNewPublisherAndSubscribes()
    {
        // Arrange
        SetupAdapter(); // Start with one publisher
        var newPublisher = Substitute.For<ITransportPublisher>();
        var command = new RollDiceCommand { GameOriginId = Guid.NewGuid() };
        _adapter.Initialize(_ => { }); // Initialize to enable subscription on add

        // Act
        _adapter.AddPublisher(newPublisher);
        _adapter.PublishCommand(command); // Publish after adding

        // Assert
        _mockPublisher1.Received(1).PublishMessage(Arg.Any<TransportMessage>()); // Original publisher receives
        newPublisher.Received(1).PublishMessage(Arg.Any<TransportMessage>()); // New publisher also receives
        newPublisher.Received(1).Subscribe(Arg.Any<Action<TransportMessage>>()); // New publisher was subscribed during Initialize/Add
    }

    [Fact]
    public void AddPublisher_DoesNotAddNull()
    {
        // Arrange
        SetupAdapter();
        var command = new RollDiceCommand { GameOriginId = Guid.NewGuid() };
        Action<IGameCommand>? callback = null;
        _adapter.Initialize(_ => callback = _ => { });
        var initialPublishCount = 0;
        _mockPublisher1.When(x => x.PublishMessage(Arg.Any<TransportMessage>()))
            .Do(_ => initialPublishCount++);

        // Act
        _adapter.AddPublisher(null);
        _adapter.PublishCommand(command);

        // Assert
        initialPublishCount.ShouldBe(1); // Only the original publisher should have received
    }

    [Fact]
    public void AddPublisher_DoesNotAddExisting()
    {
        // Arrange
        SetupAdapter();
        var command = new RollDiceCommand { GameOriginId = Guid.NewGuid() };
        Action<IGameCommand>? callback = null;
        _adapter.Initialize(_ => callback = _ => { });
        var initialPublishCount = 0;
        _mockPublisher1.When(x => x.PublishMessage(Arg.Any<TransportMessage>()))
            .Do(_ => initialPublishCount++);
        
        // Act
        _adapter.AddPublisher(_mockPublisher1); // Try adding the same publisher again
        _adapter.PublishCommand(command);

        // Assert
        initialPublishCount.ShouldBe(1); // Should still only be called once
        _mockPublisher1.Received(1).Subscribe(Arg.Any<Action<TransportMessage>>()); // Should only have been subscribed once during Initialize
    }

    [Fact]
    public void Initialize_SubscribesToAllPublishersAndDeserializesCommands()
    {
        // Arrange
        SetupAdapter(2); // Use two publishers
        var sourceId = Guid.NewGuid();
        var timestamp = DateTime.UtcNow;
        // Use a different command type for variety
        var originalCommand = new JoinGameCommand
        {
            GameOriginId = Guid.Empty,
            Timestamp = DateTime.MinValue,
            PlayerName = "Player1",
            Units = [],
            Tint = ""
        }; 
        var payload = JsonSerializer.Serialize(originalCommand);
        
        var message = new TransportMessage
        {
            MessageType = nameof(JoinGameCommand),
            SourceId = sourceId,
            Timestamp = timestamp,
            Payload = payload
        };

        Action<TransportMessage>? subscribedCallback1 = null;
        Action<TransportMessage>? subscribedCallback2 = null;
        _mockPublisher1.When(x => x.Subscribe(Arg.Any<Action<TransportMessage>>()))
            .Do(x => subscribedCallback1 = x.Arg<Action<TransportMessage>>());
        _mockPublisher2.When(x => x.Subscribe(Arg.Any<Action<TransportMessage>>()))
            .Do(x => subscribedCallback2 = x.Arg<Action<TransportMessage>>());

        IGameCommand? receivedCommand = null;
        
        // Act
        _adapter.Initialize(cmd => receivedCommand = cmd); // Call Initialize AFTER setting up When…Do
        
        // Assert Initialization subscribed to both
        _mockPublisher1.Received(1).Subscribe(Arg.Any<Action<TransportMessage>>());
        _mockPublisher2.Received(1).Subscribe(Arg.Any<Action<TransportMessage>>());
        subscribedCallback1.ShouldNotBeNull();
        subscribedCallback2.ShouldNotBeNull();

        // Act - Trigger callback on the first publisher
        subscribedCallback1!(message);

        // Assert Command Reception
        receivedCommand.ShouldNotBeNull();
        receivedCommand.ShouldBeOfType<JoinGameCommand>();
        receivedCommand.GameOriginId.ShouldBe(sourceId); // Verify ID is taken from message
        receivedCommand.Timestamp.ShouldBe(timestamp); // Verify Timestamp is taken from message
        ((JoinGameCommand)receivedCommand).PlayerName.ShouldBe("Player1");
    }

    [Fact]
    public void Initialize_WithUnknownCommandType_CallbackInvokesAndThrowsException()
    {
        // Arrange
        SetupAdapter();

        Action<TransportMessage>? subscribedCallback = null;
        _mockPublisher1.When(x => x.Subscribe(Arg.Any<Action<TransportMessage>>()))
            .Do(x => subscribedCallback = x.Arg<Action<TransportMessage>>());
        
        bool receivedCallbackCalled = false;
        // Act & Assert
        _adapter.Initialize(_ => receivedCallbackCalled = true); // Initialize first
        _mockPublisher1.Received(1).Subscribe(Arg.Any<Action<TransportMessage>>());
        subscribedCallback.ShouldNotBeNull();
        
        // Trigger the callback manually and assert exception
        receivedCallbackCalled.ShouldBeFalse(); // The final callback should not be called on error
    }

    [Fact]
    public void Initialize_WithInvalidJson_CallbackInvokesAndThrowsJsonException()
    {
        // Arrange
        SetupAdapter();

        Action<TransportMessage>? subscribedCallback = null;
        _mockPublisher1.When(x => x.Subscribe(Arg.Any<Action<TransportMessage>>()))
            .Do(x => subscribedCallback = x.Arg<Action<TransportMessage>>());
        
        var receivedCallbackCalled = false;
        
        // Act & Assert
        _adapter.Initialize(_ => receivedCallbackCalled = true);
        _mockPublisher1.Received(1).Subscribe(Arg.Any<Action<TransportMessage>>());
        subscribedCallback.ShouldNotBeNull();
        
        // Trigger the callback manually
        receivedCallbackCalled.ShouldBeFalse(); // The final callback should not be called on error
    }
    
    [Fact]
    public void DeserializeCommand_WithInvalidJson_ThrowsJsonExceptionDirectly()
    {
        // Arrange
        SetupAdapter(); // Adapter needed for its internal command type dictionary
        var message = new TransportMessage
        {
            MessageType = nameof(TurnIncrementedCommand),
            SourceId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            Payload = "{ invalid json }" // Invalid JSON payload
        };
        
        // Act & Assert
        // Directly call the internal DeserializeCommand method
        Should.Throw<JsonException>(() => _adapter.DeserializeCommand(message));
    }

    [Fact]
    public void DeserializeCommand_WithUnknownCommandType_ThrowsExceptionDirectly()
    {
        // Arrange
        SetupAdapter(); // Adapter needed for its internal command type dictionary
        var message = new TransportMessage
        {
            MessageType = "ThisCommandDoesNotExist",
            SourceId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            Payload = "{}" // Payload doesn't matter here
        };
        
        // Act & Assert
        // Directly call the internal DeserializeCommand method
        var exception = Should.Throw<UnknownCommandTypeException>(() => _adapter.DeserializeCommand(message));
        exception.CommandType.ShouldBe("ThisCommandDoesNotExist");
    }

    [Fact]
    public void Initialize_WithNoPublishers_DoesNotThrow()
    {
        // Arrange & Act
        Should.NotThrow(() => {
            var adapter = new CommandTransportAdapter(); // No publishers
            adapter.Initialize(_ => { }); // Initialize should be safe
            adapter.PublishCommand(new TurnIncrementedCommand()); // Publish should be safe (no-op)
        });
    }
    
    [Fact]
    public void ClearPublishers_DisposesAndClearsAllPublishers()
    {
        // Arrange
        var disposablePublisher1 = Substitute.For<ITransportPublisher, IDisposable>();
        var disposablePublisher2 = Substitute.For<ITransportPublisher, IDisposable>();
        var nonDisposablePublisher = Substitute.For<ITransportPublisher>();
        
        var adapter = new CommandTransportAdapter(disposablePublisher1, disposablePublisher2, nonDisposablePublisher);
        Action<IGameCommand> commandCallback = _ => {};
        adapter.Initialize(commandCallback);
        
        // Act
        adapter.ClearPublishers();
        
        // Assert
        // Verify Dispose was called on disposable publishers
        ((IDisposable)disposablePublisher1).Received(1).Dispose();
        ((IDisposable)disposablePublisher2).Received(1).Dispose();
        
        // Verify publishers list is empty by publishing a command (should not be received)
        var command = new TurnIncrementedCommand();
        adapter.PublishCommand(command);
        
        disposablePublisher1.DidNotReceive().PublishMessage(Arg.Any<TransportMessage>());
        disposablePublisher2.DidNotReceive().PublishMessage(Arg.Any<TransportMessage>());
        nonDisposablePublisher.DidNotReceive().PublishMessage(Arg.Any<TransportMessage>());
        
        // Re-add a publisher and verify we need to re-initialize
        adapter.AddPublisher(nonDisposablePublisher);
        
        // Verify we need to re-initialize since the callback was cleared
        Action<TransportMessage>? capturedCallback = null;
        nonDisposablePublisher.When(x => x.Subscribe(Arg.Any<Action<TransportMessage>>()))
            .Do(x => capturedCallback = x.Arg<Action<TransportMessage>>());
            
        // Re-initialize with a new callback
        adapter.Initialize(_ => {});
        
        capturedCallback.ShouldNotBeNull();
    }
}
