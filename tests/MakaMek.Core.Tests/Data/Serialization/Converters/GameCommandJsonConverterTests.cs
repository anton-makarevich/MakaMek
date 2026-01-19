using System.Text.Json;
using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Data.Map;
using Sanet.MakaMek.Core.Data.Serialization.Converters;
using Sanet.MakaMek.Core.Models.Game.Phases;
using Sanet.MakaMek.Core.Services.Transport;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Data.Serialization.Converters;

public class GameCommandJsonConverterTests
{
    private readonly GameCommandJsonConverter _sut;
    private readonly JsonSerializerOptions _options;

    public GameCommandJsonConverterTests()
    {
        _sut = new GameCommandJsonConverter();
        _options = new JsonSerializerOptions();
        _options.Converters.Add(_sut);
    }

    [Fact]
    public void CanConvert_IGameCommand_ReturnsTrue()
    {
        // Arrange
        var targetType = typeof(IGameCommand);

        // Act
        var result = _sut.CanConvert(targetType);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void CanConvert_OtherType_ReturnsFalse()
    {
        // Arrange
        var targetType = typeof(string);

        // Act
        var result = _sut.CanConvert(targetType);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void Write_SimpleServerCommand_WritesCorrectJson()
    {
        // Arrange
        var command = new ChangePhaseCommand
        {
            Phase = PhaseNames.Movement,
            GameOriginId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow
        };

        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);
        var options = new JsonSerializerOptions();

        // Act
        _sut.Write(writer, command, options);
        writer.Flush();
        stream.Position = 0;

        var json = new StreamReader(stream).ReadToEnd();

        // Assert
        json.ShouldContain("\"$type\":\"ChangePhaseCommand\"");
        json.ShouldContain("\"Phase\":3");
        json.ShouldContain("\"GameOriginId\"");
        json.ShouldContain("\"Timestamp\"");
    }

    [Fact]
    public void Write_ClientCommand_WritesCorrectJson()
    {
        // Arrange
        var command = new DeployUnitCommand
        {
            UnitId = Guid.NewGuid(),
            PlayerId = Guid.NewGuid(),
            GameOriginId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            Position = new HexCoordinateData(1, 2),
            Direction = 3,
            IdempotencyKey = Guid.NewGuid()
        };

        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);
        var options = new JsonSerializerOptions();

        // Act
        _sut.Write(writer, command, options);
        writer.Flush();
        stream.Position = 0;

        var json = new StreamReader(stream).ReadToEnd();

        // Assert
        json.ShouldContain("\"$type\":\"DeployUnitCommand\"");
        json.ShouldContain("\"UnitId\"");
        json.ShouldContain("\"PlayerId\"");
        json.ShouldContain("\"Position\"");
        json.ShouldContain("\"Direction\"");
        json.ShouldContain("\"IdempotencyKey\"");
    }

    [Fact]
    public void Read_ValidServerCommandJson_ReturnsCorrectCommand()
    {
        // Arrange
        var gameOriginId = Guid.NewGuid();
        var timestamp = DateTime.UtcNow;
        var json = $$"""
        {
            "$type": "ChangePhaseCommand",
            "Phase": 3,
            "GameOriginId": "{{gameOriginId}}",
            "Timestamp": "{{timestamp:O}}"
        }
        """;

        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
        var reader = new Utf8JsonReader(bytes);
        reader.Read(); // Move to StartObject

        // Act
        var result = _sut.Read(ref reader, typeof(IGameCommand), _options);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeOfType<ChangePhaseCommand>();
        
        var changePhaseCommand = (ChangePhaseCommand)result;
        changePhaseCommand.Phase.ShouldBe(PhaseNames.Movement);
        changePhaseCommand.GameOriginId.ShouldBe(gameOriginId);
        changePhaseCommand.Timestamp.ShouldBe(timestamp);
    }

    [Fact]
    public void Read_ValidClientCommandJson_ReturnsCorrectCommand()
    {
        // Arrange
        var gameOriginId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        var idempotencyKey = Guid.NewGuid();
        var timestamp = DateTime.UtcNow;
        var json = $$"""
        {
            "$type": "DeployUnitCommand",
            "UnitId": "{{unitId}}",
            "PlayerId": "{{playerId}}",
            "GameOriginId": "{{gameOriginId}}",
            "Timestamp": "{{timestamp:O}}",
            "Position": {"Q": 1, "R": 2},
            "Direction": 3,
            "IdempotencyKey": "{{idempotencyKey}}"
        }
        """;

        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
        var reader = new Utf8JsonReader(bytes);
        reader.Read(); // Move to StartObject

        // Act
        var result = _sut.Read(ref reader, typeof(IGameCommand), _options);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeOfType<DeployUnitCommand>();
        
        var deployCommand = (DeployUnitCommand)result;
        deployCommand.UnitId.ShouldBe(unitId);
        deployCommand.PlayerId.ShouldBe(playerId);
        deployCommand.GameOriginId.ShouldBe(gameOriginId);
        deployCommand.Timestamp.ShouldBe(timestamp);
        deployCommand.Position.Q.ShouldBe(1);
        deployCommand.Position.R.ShouldBe(2);
        deployCommand.Direction.ShouldBe(3);
        deployCommand.IdempotencyKey.ShouldBe(idempotencyKey);
    }

    [Fact]
    public void Read_MissingTypeProperty_ThrowsJsonException()
    {
        // Arrange
        var json = $$"""
        {
            "Phase": "Movement",
            "GameOriginId": "{{Guid.NewGuid()}}",
            "Timestamp": "{{DateTime.UtcNow:O}}"
        }
        """;

        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
        var reader = new Utf8JsonReader(bytes);
        reader.Read(); // Move to StartObject

        // Act & Assert
        JsonException? exception = null;
        try
        {
            _sut.Read(ref reader, typeof(IGameCommand), _options);
        }
        catch (JsonException ex)
        {
            exception = ex;
        }
        
        exception.ShouldNotBeNull();
        exception.Message.ShouldContain("Missing '$type' property");
    }

    [Fact]
    public void Read_EmptyTypeProperty_ThrowsJsonException()
    {
        // Arrange
        var json = $$"""
        {
            "$type": "",
            "Phase": "Movement",
            "GameOriginId": "{{Guid.NewGuid()}}",
            "Timestamp": "{{DateTime.UtcNow:O}}"
        }
        """;

        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
        var reader = new Utf8JsonReader(bytes);
        reader.Read(); // Move to StartObject

        // Act & Assert
        JsonException? exception = null;
        try
        {
            _sut.Read(ref reader, typeof(IGameCommand), _options);
        }
        catch (JsonException ex)
        {
            exception = ex;
        }
        
        exception.ShouldNotBeNull();
        exception.Message.ShouldContain("'$type' property cannot be null or empty");
    }

    [Fact]
    public void Read_UnknownTypeProperty_ThrowsJsonException()
    {
        // Arrange
        var json = $$"""
        {
            "$type": "UnknownCommand",
            "Phase": "Movement",
            "GameOriginId": "{{Guid.NewGuid()}}",
            "Timestamp": "{{DateTime.UtcNow:O}}"
        }
        """;

        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
        var reader = new Utf8JsonReader(bytes);
        reader.Read(); // Move to StartObject

        // Act & Assert
        JsonException? exception = null;
        try
        {
            _sut.Read(ref reader, typeof(IGameCommand), _options);
        }
        catch (JsonException ex)
        {
            exception = ex;
        }
        
        exception.ShouldNotBeNull();
        exception.Message.ShouldContain("Unknown command type: UnknownCommand");
    }

    [Fact]
    public void Read_InvalidJson_ReturnsNull()
    {
        // Arrange
        const string json = "{\"$type\": \"ChangePhaseCommand\", \"invalid\": }";

        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
        var reader = new Utf8JsonReader(bytes);

        // Act & 

        var command = _sut.Read(ref reader, typeof(IGameCommand), _options);

        // Assert
        command.ShouldBeNull();
    }

    [Fact]
    public void Read_InvalidCommandType_ThrowsJsonException()
    {
        // Arrange
        var json = $$"""
        {
            "$type": "ChangePhaseCommand",
            "Phase": "999",
            "GameOriginId": "{{Guid.NewGuid()}}",
            "Timestamp": "{{DateTime.UtcNow:O}}"
        }
        """;

        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
        var reader = new Utf8JsonReader(bytes);
        reader.Read(); // Move to StartObject

        // Act & Assert
        JsonException? exception = null;
        try
        {
            _sut.Read(ref reader, typeof(IGameCommand), _options);
        }
        catch (JsonException ex)
        {
            exception = ex;
        }
        
        exception.ShouldNotBeNull();
        exception.Message.ShouldContain("The JSON value could not be converted");
    }

    [Fact]
    public void Read_StartTokenNotObject_ReturnsNull()
    {
        // Arrange
        const string json = "null";

        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
        var reader = new Utf8JsonReader(bytes);

        // Act
        var result = _sut.Read(ref reader, typeof(IGameCommand), _options);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void RoundTrip_Serialization_Deserialization_WorksCorrectly()
    {
        // Arrange
        var originalCommand = new ChangePhaseCommand
        {
            Phase = PhaseNames.WeaponsAttack,
            GameOriginId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow
        };

        // Act
        var json = JsonSerializer.Serialize(originalCommand, _options);
        var deserializedCommand = JsonSerializer.Deserialize<IGameCommand>(json, _options);

        // Assert
        deserializedCommand.ShouldNotBeNull();
        deserializedCommand.ShouldBeOfType<ChangePhaseCommand>();
        
        var changePhaseCommand = (ChangePhaseCommand)deserializedCommand;
        changePhaseCommand.Phase.ShouldBe(originalCommand.Phase);
        changePhaseCommand.GameOriginId.ShouldBe(originalCommand.GameOriginId);
        changePhaseCommand.Timestamp.ShouldBe(originalCommand.Timestamp);
    }

    [Fact]
    public void RoundTrip_ClientCommand_WorksCorrectly()
    {
        // Arrange
        var originalCommand = new DeployUnitCommand
        {
            UnitId = Guid.NewGuid(),
            PlayerId = Guid.NewGuid(),
            GameOriginId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            Position = new HexCoordinateData(5, -3),
            Direction = 2,
            IdempotencyKey = Guid.NewGuid()
        };

        // Act
        var json = JsonSerializer.Serialize(originalCommand, _options);
        var deserializedCommand = JsonSerializer.Deserialize<IGameCommand>(json, _options);

        // Assert
        deserializedCommand.ShouldNotBeNull();
        deserializedCommand.ShouldBeOfType<DeployUnitCommand>();
        
        var deployCommand = (DeployUnitCommand)deserializedCommand;
        deployCommand.UnitId.ShouldBe(originalCommand.UnitId);
        deployCommand.PlayerId.ShouldBe(originalCommand.PlayerId);
        deployCommand.GameOriginId.ShouldBe(originalCommand.GameOriginId);
        deployCommand.Timestamp.ShouldBe(originalCommand.Timestamp);
        deployCommand.Position.Q.ShouldBe(originalCommand.Position.Q);
        deployCommand.Position.R.ShouldBe(originalCommand.Position.R);
        deployCommand.Direction.ShouldBe(originalCommand.Direction);
        deployCommand.IdempotencyKey.ShouldBe(originalCommand.IdempotencyKey);
    }

    [Theory]
    [InlineData("AmmoExplosionCommand")]
    [InlineData("ChangeActivePlayerCommand")]
    [InlineData("ChangePhaseCommand")]
    [InlineData("CriticalHitsResolutionCommand")]
    [InlineData("DeployUnitCommand")]
    [InlineData("DiceRolledCommand")]
    [InlineData("ErrorCommand")]
    [InlineData("GameEndedCommand")]
    [InlineData("HeatUpdatedCommand")]
    [InlineData("JoinGameCommand")]
    [InlineData("MechFallCommand")]
    [InlineData("MechStandUpCommand")]
    [InlineData("MoveUnitCommand")]
    [InlineData("PhysicalAttackCommand")]
    [InlineData("PilotConsciousnessRollCommand")]
    [InlineData("PlayerLeftCommand")]
    [InlineData("RequestGameLobbyStatusCommand")]
    [InlineData("RollDiceCommand")]
    [InlineData("SetBattleMapCommand")]
    [InlineData("ShutdownUnitCommand")]
    [InlineData("StartPhaseCommand")]
    [InlineData("StartupUnitCommand")]
    [InlineData("TryStandupCommand")]
    [InlineData("TurnEndedCommand")]
    [InlineData("TurnIncrementedCommand")]
    [InlineData("UnitShutdownCommand")]
    [InlineData("UnitStartupCommand")]
    [InlineData("UpdatePlayerStatusCommand")]
    [InlineData("WeaponAttackDeclarationCommand")]
    [InlineData("WeaponAttackResolutionCommand")]
    [InlineData("WeaponConfigurationCommand")]
    public void CommandTypeRegistry_ContainsAllCommandTypes(string commandTypeName)
    {
        // Act
        var type = CommandTypeRegistry.GetCommandType(commandTypeName);

        // Assert
        type.ShouldNotBeNull();
        type.Name.ShouldBe(commandTypeName);
    }

    [Fact]
    public void CommandTypeRegistry_GetCommandTypeName_ReturnsCorrectName()
    {
        // Arrange
        var commandType = typeof(ChangePhaseCommand);

        // Act
        var typeName = CommandTypeRegistry.GetCommandTypeName(commandType);

        // Assert
        typeName.ShouldBe("ChangePhaseCommand");
    }

    [Fact]
    public void CommandTypeRegistry_UnknownType_ReturnsNull()
    {
        // Arrange
        const string unknownTypeName = "NonExistentCommand";

        // Act
        var type = CommandTypeRegistry.GetCommandType(unknownTypeName);

        // Assert
        type.ShouldBeNull();
    }
}