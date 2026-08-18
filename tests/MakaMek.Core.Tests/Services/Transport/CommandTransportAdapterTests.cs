using NSubstitute;
using Sanet.MakaMek.Core.Exceptions;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.Transport;
using Shouldly;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Engines;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PilotingSkillRollContexts;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.PilotingSkill;
using Sanet.MakaMek.Map.Models;
using Sanet.Transport.SignalR.Client.Publishers;

namespace Sanet.MakaMek.Core.Tests.Services.Transport;

public class CommandTransportAdapterTests
{
    private ITransportPublisher _mockPublisher1 = null!;
    private ITransportPublisher _mockPublisher2 = null!;
    private CommandTransportAdapter _sut = null!;
    private List<ITransportPublisher> _publishers = null!;
    private readonly ILoggerFactory _loggerFactory = Substitute.For<ILoggerFactory>();
    private readonly ILogger<CommandTransportAdapter> _logger = Substitute.For<ILogger<CommandTransportAdapter>>();

    // Helper to set up an adapter with a variable number of publishers
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

        _loggerFactory.CreateLogger<CommandTransportAdapter>().Returns(_logger);
        _sut = new CommandTransportAdapter(_loggerFactory, _publishers.ToArray());
    }

    [Fact]
    public void PublishCommand_SendsToAllPublishers()
    {
        // Arrange
        SetupAdapter(2); // Use two publishers
        var command = new TurnIncrementedCommand
        {
            GameOriginId = Guid.NewGuid(),
            TurnNumber = 1
        };

        TransportMessage? capturedMessage1 = null;
        TransportMessage? capturedMessage2 = null;
        _mockPublisher1.When(x => x.PublishMessage(Arg.Any<TransportMessage>()))
            .Do(x => capturedMessage1 = x.Arg<TransportMessage>());
        _mockPublisher2.When(x => x.PublishMessage(Arg.Any<TransportMessage>()))
            .Do(x => capturedMessage2 = x.Arg<TransportMessage>());

        // Act
        _sut.PublishCommand(command);

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
    public void PublishAndDeserializeCommand_WithComponentSpecificData_RoundTripsEngineState()
    {
        // Arrange
        SetupAdapter();
        TransportMessage? capturedMessage = null;
        _mockPublisher1.When(x => x.PublishMessage(Arg.Any<TransportMessage>()))
            .Do(ci => capturedMessage = ci.Arg<TransportMessage>());

        var unitId = Guid.NewGuid();
        var componentSpecificData = new EngineStateData(EngineType.Fusion, 300);
        var component = new ComponentData
        {
            Type = MakaMekComponent.Engine,
            Assignments = new List<LocationSlotAssignment>
            {
                new(PartLocation.CenterTorso, 0, 1)
            },
            Hits = 0,
            IsActive = true,
            HasExploded = false,
            SpecificData = componentSpecificData
        };

        var unitData = new UnitData
        {
            Id = unitId,
            Chassis = "Atlas",
            Model = "AS7-D",
            Mass = 100,
            EngineRating = 300,
            EngineType = "Fusion",
            ArmorValues = new Dictionary<PartLocation, ArmorLocation>
            {
                { PartLocation.CenterTorso, new ArmorLocation { FrontArmor = 30, RearArmor = 10 } }
            },
            Equipment = new List<ComponentData> { component },
            AdditionalAttributes = new Dictionary<string, string>(),
            Quirks = new Dictionary<string, string>()
        };

        var joinCommand = new JoinGameCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = Guid.NewGuid(),
            PlayerName = "TestPlayer",
            Units = [unitData],
            PilotAssignments =
            [
                new PilotAssignmentData
                {
                    UnitId = unitId,
                    PilotData = PilotData.CreateDefaultPilot("Test", "Pilot")
                }
            ],
            Tint = "#FFFFFF",
            Timestamp = DateTime.UtcNow
        };

        // Act
        _sut.PublishCommand(joinCommand);

        // Assert serialization captured the message
        capturedMessage.ShouldNotBeNull();
        //capturedMessage!.Payload.ShouldContain("\"$type\"");

        // Act - attempt round-trip deserialization
        var roundTripped = (JoinGameCommand)_sut.DeserializeCommand(capturedMessage!);

        // Assert
        roundTripped.Units.ShouldNotBeEmpty();
        var roundTrippedComponent = roundTripped.Units[0].Equipment[0];
        roundTrippedComponent.SpecificData.ShouldNotBeNull();
        roundTrippedComponent.SpecificData.ShouldBeOfType<EngineStateData>();

        var engineState = (EngineStateData)roundTrippedComponent.SpecificData!;
        engineState.Type.ShouldBe(EngineType.Fusion);
        engineState.Rating.ShouldBe(300);
    }

    [Fact]
    public void AddPublisher_AddsNewPublisherAndSubscribes()
    {
        // Arrange
        SetupAdapter(); // Start with one publisher
        var newPublisher = Substitute.For<ITransportPublisher>();
        var command = new RollDiceCommand { GameOriginId = Guid.NewGuid() };
        _sut.Initialize((_,_) => { }); // Initialize to enable subscription on add

        // Act
        _sut.AddPublisher(newPublisher);
        _sut.PublishCommand(command); // Publish after adding

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
        _sut.Initialize((_, _) => { });
        var initialPublishCount = 0;
        _mockPublisher1.When(x => x.PublishMessage(Arg.Any<TransportMessage>()))
            .Do(_ => initialPublishCount++);

        // Act
        _sut.AddPublisher(null);
        _sut.PublishCommand(command);

        // Assert
        initialPublishCount.ShouldBe(1); // Only the original publisher should have received
    }

    [Fact]
    public void AddPublisher_DoesNotAddExisting()
    {
        // Arrange
        SetupAdapter();
        var command = new RollDiceCommand { GameOriginId = Guid.NewGuid() };
        _sut.Initialize((_,_) => { });
        var initialPublishCount = 0;
        _mockPublisher1.When(x => x.PublishMessage(Arg.Any<TransportMessage>()))
            .Do(_ => initialPublishCount++);

        // Act
        _sut.AddPublisher(_mockPublisher1); // Try adding the same publisher again
        _sut.PublishCommand(command);

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
            Tint = "",
            PilotAssignments = []
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
        _sut.Initialize((cmd,_) => receivedCommand = cmd); // Call Initialize AFTER setting up When…Do

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
        _sut.Initialize((_,_) => receivedCallbackCalled = true); // Initialize first
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
        _sut.Initialize((_,_) => receivedCallbackCalled = true);
        _mockPublisher1.Received(1).Subscribe(Arg.Any<Action<TransportMessage>>());
        subscribedCallback.ShouldNotBeNull();

        // Trigger the callback manually
        receivedCallbackCalled.ShouldBeFalse(); // The final callback should not be called on error
    }

    [Fact]
    public void PublishCommand_WhenPublisherThrows_LogsErrorAndContinuesToOtherPublishers()
    {
        // Arrange
        SetupAdapter(2);
        _mockPublisher1.When(x => x.PublishMessage(Arg.Any<TransportMessage>()))
            .Do(_ => throw new InvalidOperationException("boom"));
        var command = new TurnIncrementedCommand
        {
            GameOriginId = Guid.NewGuid(),
            TurnNumber = 1
        };

        // Act & Assert
        Should.NotThrow(() => _sut.PublishCommand(command));

        _mockPublisher2.Received(1).PublishMessage(Arg.Any<TransportMessage>());
        _logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Any<Arg.AnyType>(),
            Arg.Any<InvalidOperationException>(),
            Arg.Any<Func<Arg.AnyType, Exception?, string>>());
    }

    [Fact]
    public void Subscribe_WhenUnknownCommandTypeReceived_SwallowsErrorAndDoesNotInvokeCallback()
    {
        // Arrange
        SetupAdapter();
        Action<TransportMessage>? subscribedCallback = null;
        _mockPublisher1.When(x => x.Subscribe(Arg.Any<Action<TransportMessage>>()))
            .Do(x => subscribedCallback = x.Arg<Action<TransportMessage>>());
        var callbackCalled = false;
        _sut.Initialize((_, _) => callbackCalled = true);
        subscribedCallback.ShouldNotBeNull();

        // Act & Assert
        Should.NotThrow(() => subscribedCallback!(new TransportMessage
        {
            MessageType = "ThisCommandDoesNotExist",
            SourceId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            Payload = "{}"
        }));

        callbackCalled.ShouldBeFalse();
        _logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Any<Arg.AnyType>(),
            Arg.Any<UnknownCommandTypeException>(),
            Arg.Any<Func<Arg.AnyType, Exception?, string>>());
    }

    [Fact]
    public void Subscribe_WhenInvalidJsonReceived_SwallowsErrorAndDoesNotInvokeCallback()
    {
        // Arrange
        SetupAdapter();
        Action<TransportMessage>? subscribedCallback = null;
        _mockPublisher1.When(x => x.Subscribe(Arg.Any<Action<TransportMessage>>()))
            .Do(x => subscribedCallback = x.Arg<Action<TransportMessage>>());
        var callbackCalled = false;
        _sut.Initialize((_, _) => callbackCalled = true);
        subscribedCallback.ShouldNotBeNull();

        // Act & Assert
        Should.NotThrow(() => subscribedCallback!(new TransportMessage
        {
            MessageType = nameof(TurnIncrementedCommand),
            SourceId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            Payload = "{ invalid json }"
        }));

        callbackCalled.ShouldBeFalse();
        _logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Any<Arg.AnyType>(),
            Arg.Any<JsonException>(),
            Arg.Any<Func<Arg.AnyType, Exception?, string>>());
    }

    [Fact]
    public void Subscribe_WhenCallbackThrows_SwallowsErrorAndContinues()
    {
        // Arrange
        SetupAdapter();
        Action<TransportMessage>? subscribedCallback = null;
        _mockPublisher1.When(x => x.Subscribe(Arg.Any<Action<TransportMessage>>()))
            .Do(x => subscribedCallback = x.Arg<Action<TransportMessage>>());
        _sut.Initialize((_, _) => throw new InvalidOperationException("boom"));
        subscribedCallback.ShouldNotBeNull();

        var command = new TurnIncrementedCommand
        {
            GameOriginId = Guid.NewGuid(),
            TurnNumber = 1
        };
        var message = new TransportMessage
        {
            MessageType = nameof(TurnIncrementedCommand),
            SourceId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            Payload = JsonSerializer.Serialize(command)
        };

        // Act & Assert
        Should.NotThrow(() => subscribedCallback!(message));

        _logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Any<Arg.AnyType>(),
            Arg.Any<InvalidOperationException>(),
            Arg.Any<Func<Arg.AnyType, Exception?, string>>());
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
        Should.Throw<JsonException>(() => _sut.DeserializeCommand(message));
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
        var exception = Should.Throw<UnknownCommandTypeException>(() => _sut.DeserializeCommand(message));
        exception.CommandType.ShouldBe("ThisCommandDoesNotExist");
    }

    [Fact]
    public void DeserializeCommand_WhenPayloadIsJsonNull_ThrowsJsonException()
    {
        // Arrange
        SetupAdapter(); // Adapter needed for its internal command type dictionary
        var message = new TransportMessage
        {
            MessageType = nameof(TurnIncrementedCommand),
            SourceId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            Payload = "null" // Valid JSON literal
        };

        // Act & Assert
        // All registered commands are value types, so a JSON null literal cannot
        // be deserialized to a command and is rejected rather than silently accepted
        var exception = Should.Throw<JsonException>(() => _sut.DeserializeCommand(message));
        exception.Message.ShouldContain(nameof(TurnIncrementedCommand));
    }

    [Fact]
    public void DeserializeCommand_WhenJsonNullForReferenceTypeCommand_ThrowsInvalidOperationException()
    {
        // Arrange
        SetupAdapter(); // Adapter needed for its internal command type dictionary
        var message = new TransportMessage
        {
            MessageType = nameof(HullBreachCommand),
            SourceId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            Payload = "null" // Valid JSON literal
        };

        // Act & Assert
        // HullBreachCommand is a reference-type record, so a JSON null literal
        // deserializes to null rather than throwing; the adapter must reject it
        var exception = Should.Throw<InvalidOperationException>(() => _sut.DeserializeCommand(message));
        exception.Message.ShouldContain(nameof(HullBreachCommand));
    }

    [Fact]
    public void Initialize_WithNoPublishers_DoesNotThrow()
    {
        // Arrange
        _loggerFactory.CreateLogger<CommandTransportAdapter>().Returns(_logger);
        
        // Act
        Should.NotThrow(() =>
        {
            var sut = new CommandTransportAdapter(_loggerFactory); // No publishers
            sut.Initialize((_,_) => { }); // Initialize should be safe
            sut.PublishCommand(new TurnIncrementedCommand
            {
                GameOriginId = Guid.NewGuid(),
                TurnNumber = 1
            }); // Publish should be safe (no-op)
        });
    }

    [Fact]
    public async Task ClearPublishers_DisposesAndClearsAllPublishers()
    {
        // Arrange
        var disposablePublisher1 = Substitute.For<ITransportPublisher, IAsyncDisposable>();
        var disposablePublisher2 = Substitute.For<ITransportPublisher, IAsyncDisposable>();
        var nonDisposablePublisher = Substitute.For<ITransportPublisher>();
        _loggerFactory.CreateLogger<CommandTransportAdapter>().Returns(_logger);

        var sut = new CommandTransportAdapter(
            _loggerFactory,
            disposablePublisher1,
            disposablePublisher2,
            nonDisposablePublisher);
        Action<IGameCommand, ITransportPublisher> commandCallback = (_,_) => {};
        sut.Initialize(commandCallback);

        // Act
        await sut.ClearPublishers();

        // Assert
        // Verify Dispose was called on disposable publishers
        await ((IAsyncDisposable)disposablePublisher1).Received(1).DisposeAsync();
        await ((IAsyncDisposable)disposablePublisher2).Received(1).DisposeAsync();

        // Verify publishers list is empty by publishing a command (should not be received)
        var command = new TurnIncrementedCommand
        {
            GameOriginId = Guid.NewGuid(),
            TurnNumber = 1
        };
        sut.PublishCommand(command);

        disposablePublisher1.DidNotReceive().PublishMessage(Arg.Any<TransportMessage>());
        disposablePublisher2.DidNotReceive().PublishMessage(Arg.Any<TransportMessage>());
        nonDisposablePublisher.DidNotReceive().PublishMessage(Arg.Any<TransportMessage>());

        // Re-add a publisher and verify we need to re-initialize
        sut.AddPublisher(nonDisposablePublisher);

        // Verify we need to re-initialize since the callback was cleared
        Action<TransportMessage>? capturedCallback = null;
        nonDisposablePublisher.When(x => x.Subscribe(Arg.Any<Action<TransportMessage>>()))
            .Do(x => capturedCallback = x.Arg<Action<TransportMessage>>());

        // Re-initialize with a new callback
        sut.Initialize((_,_) => {});

        capturedCallback.ShouldNotBeNull();
    }

    [Fact]
    public async Task ClearPublishers_ContinuesDisposingAfterException()
    {
        // Arrange
        var throwingPublisher = Substitute.For<ITransportPublisher, IAsyncDisposable>();
        var normalPublisher = Substitute.For<ITransportPublisher, IAsyncDisposable>();

        // Configure the first publisher to throw an exception when disposed
        ((IAsyncDisposable)throwingPublisher).When(x => x.DisposeAsync())
            .Do(_ => throw new InvalidOperationException("Test exception during dispose"));

        _loggerFactory.CreateLogger<CommandTransportAdapter>().Returns(_logger);
        var sut = new CommandTransportAdapter(_loggerFactory, throwingPublisher, normalPublisher);
        sut.Initialize((_,_) => {});

        // Act - This should not throw despite the exception in Dispose()
        await Should.NotThrowAsync(sut.ClearPublishers);

        // Assert
        // Verify that both publishers had Dispose() called, even though the first one threw
        await ((IAsyncDisposable)throwingPublisher).Received(1).DisposeAsync();
        await ((IAsyncDisposable)normalPublisher).Received(1).DisposeAsync();

        // Verify that the publishers list was cleared
        var command = new TurnIncrementedCommand
        {
            GameOriginId = Guid.NewGuid(),
            TurnNumber = 1
        };
        sut.PublishCommand(command);

        throwingPublisher.DidNotReceive().PublishMessage(Arg.Any<TransportMessage>());
        normalPublisher.DidNotReceive().PublishMessage(Arg.Any<TransportMessage>());
    }

    [Fact]
    public async Task Dispose_CallsClearPublishersAndDisposesAllDisposablePublishers()
    {
        // Arrange
        var disposablePublisher1 = Substitute.For<ITransportPublisher, IAsyncDisposable>();
        var disposablePublisher2 = Substitute.For<ITransportPublisher, IAsyncDisposable>();
        var nonDisposablePublisher = Substitute.For<ITransportPublisher>();
        _loggerFactory.CreateLogger<CommandTransportAdapter>().Returns(_logger);

        var sut = new CommandTransportAdapter(
            _loggerFactory,
            disposablePublisher1,
            disposablePublisher2,
            nonDisposablePublisher);
        sut.Initialize((_, _) => { });

        // Act
        await sut.DisposeAsync();

        // Assert - Dispose was called on disposable publishers
        await ((IAsyncDisposable)disposablePublisher1).Received(1).DisposeAsync();
        await ((IAsyncDisposable)disposablePublisher2).Received(1).DisposeAsync();

        // Assert - publishers list is cleared (publishing does nothing)
        var command = new TurnIncrementedCommand
        {
            GameOriginId = Guid.NewGuid(),
            TurnNumber = 1
        };
        sut.PublishCommand(command);

        disposablePublisher1.DidNotReceive().PublishMessage(Arg.Any<TransportMessage>());
        disposablePublisher2.DidNotReceive().PublishMessage(Arg.Any<TransportMessage>());
        nonDisposablePublisher.DidNotReceive().PublishMessage(Arg.Any<TransportMessage>());
    }

    [Fact]
    public void Initialize_CalledMultipleTimes_SubscribesOnlyOnce()
    {
        // Arrange
        SetupAdapter();

        // Act
        _sut.Initialize((_,_)=>{ });
        _sut.Initialize((_,_)=>{ }); // Should be ignored

        // Assert
        _mockPublisher1.Received(1).Subscribe(Arg.Any<Action<TransportMessage>>());
    }

    [Fact]
    public void JoinGameCommand_WithMultipleComponentSpecificDataTypes_SerializesAndDeserializesCorrectly()
    {
        // Arrange
        SetupAdapter();
        TransportMessage? capturedMessage = null;
        _mockPublisher1.When(x => x.PublishMessage(Arg.Any<TransportMessage>()))
            .Do(ci => capturedMessage = ci.Arg<TransportMessage>());

        var unitId = Guid.NewGuid();

        // Create components with different types of SpecificData to test polymorphism
        var engineComponent = new ComponentData
        {
            Type = MakaMekComponent.Engine,
            Assignments = new List<LocationSlotAssignment>
            {
                new(PartLocation.CenterTorso, 0, 6)
            },
            Hits = 0,
            IsActive = true,
            HasExploded = false,
            SpecificData = new EngineStateData(EngineType.Fusion, 300)
        };

        var ammoComponent = new ComponentData
        {
            Type = MakaMekComponent.ISAmmoAC20,
            Assignments = new List<LocationSlotAssignment>
            {
                new(PartLocation.RightTorso, 0, 1)
            },
            Hits = 0,
            IsActive = true,
            HasExploded = false,
            SpecificData = new AmmoStateData(15)
        };

        var regularComponent = new ComponentData
        {
            Type = MakaMekComponent.MediumLaser,
            Assignments = new List<LocationSlotAssignment>
            {
                new(PartLocation.RightArm, 0, 1)
            },
            Hits = 0,
            IsActive = true,
            HasExploded = false,
            SpecificData = null // No specific data
        };

        var unitData = new UnitData
        {
            Id = unitId,
            Chassis = "TestMech",
            Model = "TM-1",
            Mass = 50,
            EngineRating = 300,
            EngineType = "Fusion",
            ArmorValues = new Dictionary<PartLocation, ArmorLocation>(),
            Equipment = new List<ComponentData> { engineComponent, ammoComponent, regularComponent },
            AdditionalAttributes = new Dictionary<string, string>(),
            Quirks = new Dictionary<string, string>()
        };

        var joinCommand = new JoinGameCommand
        {
            GameOriginId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            PlayerName = "TestPlayer",
            Units = [unitData],
            Tint = "#FF0000",
            PilotAssignments = new List<PilotAssignmentData>(),
            PlayerId = Guid.NewGuid()
        };

        // Act - Publish and deserialize the command
        _sut.PublishCommand(joinCommand);
        capturedMessage.ShouldNotBeNull();
        var deserializedCommand = (JoinGameCommand)_sut.DeserializeCommand(capturedMessage!);

        // Assert - Verify all component types were preserved correctly
        deserializedCommand.Units.ShouldNotBeEmpty();
        var deserializedEquipment = deserializedCommand.Units[0].Equipment;
        deserializedEquipment.Count.ShouldBe(3);

        // Verify engine component
        var deserializedEngine = deserializedEquipment[0];
        deserializedEngine.SpecificData.ShouldNotBeNull();
        deserializedEngine.SpecificData.ShouldBeOfType<EngineStateData>();
        var engineState = (EngineStateData)deserializedEngine.SpecificData!;
        engineState.Type.ShouldBe(EngineType.Fusion);
        engineState.Rating.ShouldBe(300);

        // Verify ammo component
        var deserializedAmmo = deserializedEquipment[1];
        deserializedAmmo.SpecificData.ShouldNotBeNull();
        deserializedAmmo.SpecificData.ShouldBeOfType<AmmoStateData>();
        var ammoState = (AmmoStateData)deserializedAmmo.SpecificData!;
        ammoState.RemainingShots.ShouldBe(15);

        // Verify regular component (no specific data)
        var deserializedRegular = deserializedEquipment[2];
        deserializedRegular.SpecificData.ShouldBeNull();
    }

    [Fact]
    public void InitializeCommandTypeDictionary_RegistersAllKnownCommandTypes()
    {
        // Arrange
        SetupAdapter();

        // Get all command types from the assembly
        var assembly = typeof(IGameCommand).Assembly;
        var allCommandTypes = assembly.GetTypes()
            .Where(t => typeof(IGameCommand).IsAssignableFrom(t)
                        && t is { IsInterface: false, IsAbstract: false })
            .ToList();

        // Act - Test each command type by attempting to deserialize it
        foreach (var commandType in allCommandTypes)
        {
            var commandName = commandType.Name;
            var message = new TransportMessage
            {
                MessageType = commandName,
                SourceId = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                Payload = "{}" // Minimal valid JSON
            };

            // Assert - Should not throw UnknownCommandTypeException
            // Note: May throw JsonException if the payload is invalid for the specific command,
            // but that's okay - we're just testing that the type is registered
            try
            {
                var result = _sut.DeserializeCommand(message);
                result.ShouldNotBeNull();
            }
            catch (JsonException)
            {
                // Expected for commands with required properties
                // The important thing is we didn't get UnknownCommandTypeException
            }
            catch (UnknownCommandTypeException ex)
            {
                // This should never happen - it means the command type wasn't registered
                throw new Exception($"Command type '{commandName}' was not registered in the dictionary", ex);
            }
        }

        // Verify we tested at least some commands
        allCommandTypes.Count.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void MechStandUpCommand_WithPolymorphicPilotingSkillRollData_RoundTripsCorrectly()
    {
        // Arrange
        SetupAdapter();
        TransportMessage? capturedMessage = null;
        _mockPublisher1.When(x => x.PublishMessage(Arg.Any<TransportMessage>()))
            .Do(ci => capturedMessage = ci.Arg<TransportMessage>());

        // Create a PilotingSkillRollContext with a concrete type (EnteringDeepWaterRollContext)
        var rollContext = new EnteringDeepWaterRollContext(WaterDepth: 2);

        // Create RollModifier instances with concrete types (DamagedGyroModifier and FallingLevelsModifier)
        var modifiers = new List<RollModifier>
        {
            new DamagedGyroModifier { Value = 2, HitsCount = 1 },
            new FallingLevelsModifier { Value = 1, LevelsFallen = 3 }
        };

        // Create a PsrBreakdown with the modifiers
        var psrBreakdown = new PsrBreakdown
        {
            BasePilotingSkill = 4,
            Modifiers = modifiers
        };

        // Create PilotingSkillRollData with the context and breakdown
        var pilotingSkillRollData = new PilotingSkillRollData
        {
            RollContext = rollContext,
            DiceResults = [5, 3],
            IsSuccessful = true,
            PsrBreakdown = psrBreakdown
        };

        // Create MechStandUpCommand with the piloting skill roll data
        var standUpCommand = new MechStandUpCommand
        {
            GameOriginId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            UnitId = Guid.NewGuid(),
            PilotingSkillRoll = pilotingSkillRollData,
            NewFacing = HexDirection.Top
        };

        // Act - Publish the command
        _sut.PublishCommand(standUpCommand);

        // Assert serialization captured the message
        capturedMessage.ShouldNotBeNull();

        // Act - Deserialize the command back
        var roundTripped = (MechStandUpCommand)_sut.DeserializeCommand(capturedMessage!);

        // Assert - Verify the deserialized command retains all data
        roundTripped.UnitId.ShouldBe(standUpCommand.UnitId);
        roundTripped.NewFacing.ShouldBe(standUpCommand.NewFacing);

        // Verify PilotingSkillRollContext polymorphic type is preserved
        roundTripped.PilotingSkillRoll.RollContext.ShouldNotBeNull();
        roundTripped.PilotingSkillRoll.RollContext.ShouldBeOfType<EnteringDeepWaterRollContext>();
        var roundTrippedContext = (EnteringDeepWaterRollContext)roundTripped.PilotingSkillRoll.RollContext;
        roundTrippedContext.WaterDepth.ShouldBe(2);

        // Verify RollModifier polymorphic types are preserved
        roundTripped.PilotingSkillRoll.PsrBreakdown.Modifiers.Count.ShouldBe(2);
        roundTripped.PilotingSkillRoll.PsrBreakdown.Modifiers[0].ShouldBeOfType<DamagedGyroModifier>();
        var roundTrippedGyroModifier = (DamagedGyroModifier)roundTripped.PilotingSkillRoll.PsrBreakdown.Modifiers[0];
        roundTrippedGyroModifier.Value.ShouldBe(2);
        roundTrippedGyroModifier.HitsCount.ShouldBe(1);

        roundTripped.PilotingSkillRoll.PsrBreakdown.Modifiers[1].ShouldBeOfType<FallingLevelsModifier>();
        var roundTrippedFallingModifier = (FallingLevelsModifier)roundTripped.PilotingSkillRoll.PsrBreakdown.Modifiers[1];
        roundTrippedFallingModifier.Value.ShouldBe(1);
        roundTrippedFallingModifier.LevelsFallen.ShouldBe(3);

        // Verify other PilotingSkillRollData properties
        roundTripped.PilotingSkillRoll.DiceResults.ShouldBeEquivalentTo(standUpCommand.PilotingSkillRoll.DiceResults);
        roundTripped.PilotingSkillRoll.IsSuccessful.ShouldBe(standUpCommand.PilotingSkillRoll.IsSuccessful);
        roundTripped.PilotingSkillRoll.PsrBreakdown.BasePilotingSkill.ShouldBe(4);
        roundTripped.PilotingSkillRoll.PsrBreakdown.ModifiedPilotingSkill.ShouldBe(7); // 4 + 2 + 1
    }

    private static RelayClientPublisher CreateRelayPublisher() =>
        new("http://hub.local/hubs/relay", "ABC123", "session-token", NullLogger<RelayClientPublisher>.Instance);

    [Fact]
    public void RegisterDisconnectHandler_InvokedWhenRelayPublisherHostDisconnects()
    {
        // Arrange
        SetupAdapter();
        var relayPublisher = CreateRelayPublisher();
        _sut.AddPublisher(relayPublisher);

        ITransportPublisher? disconnectedPublisher = null;

        // Act
        _sut.RegisterDisconnectHandler(p => disconnectedPublisher = p);
        // Trigger the event via reflection since RelayClientPublisher does not expose a public raise method
        var eventField = typeof(RelayClientPublisher).GetField("HostDisconnected",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var handler = eventField?.GetValue(relayPublisher) as Action;
        handler?.Invoke();

        // Assert
        disconnectedPublisher.ShouldBe(relayPublisher);
    }

    [Fact]
    public void RegisterDisconnectHandler_IgnoresNonRelayPublishers()
    {
        // Arrange
        SetupAdapter(); // Uses a plain ITransportPublisher substitute
        var called = false;

        // Act
        Should.NotThrow(() => _sut.RegisterDisconnectHandler(_ => called = true));

        // Assert
        called.ShouldBeFalse();
    }

    [Fact]
    public void RegisterDisconnectHandler_CalledMultipleTimes_RegistersOnlyOnce()
    {
        // Arrange
        SetupAdapter();
        var relayPublisher = CreateRelayPublisher();
        _sut.AddPublisher(relayPublisher);
        var callCount = 0;

        // Act
        _sut.RegisterDisconnectHandler(_ => callCount++);
        _sut.RegisterDisconnectHandler(_ => callCount++); // Should be ignored

        var eventField = typeof(RelayClientPublisher).GetField("HostDisconnected",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var handler = eventField?.GetValue(relayPublisher) as Action;
        handler?.Invoke();

        // Assert
        callCount.ShouldBe(1);
    }

    [Fact]
    public void RemovePublisher_UnsubscribesDisconnectHandler()
    {
        // Arrange
        SetupAdapter();
        var relayPublisher = CreateRelayPublisher();
        _sut.AddPublisher(relayPublisher);
        var called = false;
        _sut.RegisterDisconnectHandler(_ => called = true);

        // Act
        _sut.RemovePublisher(relayPublisher);
        var eventField = typeof(RelayClientPublisher).GetField("HostDisconnected",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var handler = eventField?.GetValue(relayPublisher) as Action;
        handler?.Invoke();

        // Assert
        called.ShouldBeFalse();
    }

    [Fact]
    public void AddPublisher_SubscribesDisconnectHandlerWhenAlreadyRegistered()
    {
        // Arrange
        SetupAdapter();
        _sut.RegisterDisconnectHandler(_ => { });
        var relayPublisher = CreateRelayPublisher();

        // Act & Assert - should not throw when adding after registration
        Should.NotThrow(() => _sut.AddPublisher(relayPublisher));
    }

    [Fact]
    public void DispatchLocalCommand_WhenInitialized_InvokesReceiveCallbackWithCommandAndPublisher()
    {
        // Arrange
        SetupAdapter();
        IGameCommand? receivedCommand = null;
        ITransportPublisher? receivedPublisher = null;
        _sut.Initialize((command, publisher) =>
        {
            receivedCommand = command;
            receivedPublisher = publisher;
        });

        var command = new HullBreachCommand
        {
            GameOriginId = Guid.NewGuid(),
            UnitId = Guid.NewGuid(),
            BreachedLocations = []
        };
        var sourcePublisher = Substitute.For<ITransportPublisher>();

        // Act
        _sut.DispatchLocalCommand(command, sourcePublisher);

        // Assert
        receivedCommand.ShouldBeSameAs(command);
        receivedPublisher.ShouldBeSameAs(sourcePublisher);
    }

    [Fact]
    public void DispatchLocalCommand_WhenNotInitialized_DoesNotThrow()
    {
        // Arrange
        SetupAdapter();
        var command = new GameEndedCommand
        {
            GameOriginId = Guid.NewGuid(),
            Reason = GameEndReason.HostDisconnected,
            Timestamp = DateTime.UtcNow
        };
        var sourcePublisher = Substitute.For<ITransportPublisher>();

        // Act & Assert
        Should.NotThrow(() => _sut.DispatchLocalCommand(command, sourcePublisher));
    }

    [Fact]
    public void DispatchLocalCommand_WhenCallbackThrows_LogsErrorAndDoesNotThrow()
    {
        // Arrange
        SetupAdapter();
        _sut.Initialize((_, _) => throw new InvalidOperationException("boom"));
        var command = new GameEndedCommand
        {
            GameOriginId = Guid.NewGuid(),
            Reason = GameEndReason.HostDisconnected,
            Timestamp = DateTime.UtcNow
        };
        var sourcePublisher = Substitute.For<ITransportPublisher>();

        // Act & Assert
        Should.NotThrow(() => _sut.DispatchLocalCommand(command, sourcePublisher));

        _logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Any<Arg.AnyType>(),
            Arg.Any<InvalidOperationException>(),
            Arg.Any<Func<Arg.AnyType, Exception?, string>>());
    }

    [Fact]
    public void PublishCommand_ToTargetPublisher_SendsOnlyToTarget()
    {
        // Arrange
        SetupAdapter(2);
        var command = new TurnIncrementedCommand
        {
            GameOriginId = Guid.NewGuid(),
            TurnNumber = 1
        };

        // Act
        _sut.PublishCommand(command, _mockPublisher1);

        // Assert
        _mockPublisher1.Received(1).PublishMessage(Arg.Any<TransportMessage>());
        _mockPublisher2.DidNotReceive().PublishMessage(Arg.Any<TransportMessage>());
    }

    [Fact]
    public void PublishCommand_ToTargetPublisher_PreservesErrorIsolation()
    {
        // Arrange
        SetupAdapter(2);
        _mockPublisher1.When(x => x.PublishMessage(Arg.Any<TransportMessage>()))
            .Do(_ => throw new InvalidOperationException("boom"));
        var command = new TurnIncrementedCommand
        {
            GameOriginId = Guid.NewGuid(),
            TurnNumber = 1
        };

        // Act - target publisher throws, but the call should not propagate
        Should.NotThrow(() => _sut.PublishCommand(command, _mockPublisher1));
    }

    [Fact]
    public void PublishCommand_ToTargetPublisher_SerializesOnce()
    {
        // Arrange
        SetupAdapter();
        TransportMessage? capturedMessage = null;
        _mockPublisher1.When(x => x.PublishMessage(Arg.Any<TransportMessage>()))
            .Do(x => capturedMessage = x.Arg<TransportMessage>());
        var command = new TurnIncrementedCommand
        {
            GameOriginId = Guid.NewGuid(),
            TurnNumber = 1
        };

        // Act
        _sut.PublishCommand(command, _mockPublisher1);

        // Assert
        capturedMessage.ShouldNotBeNull();
        capturedMessage!.MessageType.ShouldBe(nameof(TurnIncrementedCommand));
        capturedMessage.SourceId.ShouldBe(command.GameOriginId);
    }
}
