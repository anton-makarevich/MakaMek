using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Models.Game.Phases;
using Sanet.MakaMek.Core.Services.Cryptography;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Services.Cryptography;

public class HashServiceTests
{
    private readonly HashService _sut = new();
    
    [Fact]
    public void ComputeCommandIdempotencyKey_ShouldReturnSameKey_ForSameInputs()
    {
        // Arrange
        var playerId = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var commandType = typeof(DeployUnitCommand);

        // Act
        var key1 = _sut.ComputeCommandIdempotencyKey(gameId,
            playerId,
            commandType,
            1, 
            nameof(PhaseNames.Deployment),
            unitId);
        var key2 = _sut.ComputeCommandIdempotencyKey(
            gameId,
            playerId,
            commandType,
            1, 
            nameof(PhaseNames.Deployment),
            unitId);

        // Assert
        key1.ShouldBe(key2);
    }

    [Fact]
    public void ComputeCommandIdempotencyKey_ShouldReturnDifferentKeys_ForDifferentPlayers()
    {
        // Arrange
        var playerId1 = Guid.NewGuid();
        var playerId2 = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var commandType = typeof(DeployUnitCommand);

        // Act
        var key1 = _sut.ComputeCommandIdempotencyKey(
            gameId,
            playerId1,
            commandType,
            1, 
            nameof(PhaseNames.Deployment),
            unitId);
        var key2 = _sut.ComputeCommandIdempotencyKey(
            gameId,
            playerId2,
            commandType,
            1, 
            nameof(PhaseNames.Deployment),
            unitId);

        // Assert
        key1.ShouldNotBe(key2);
    }

    [Fact]
    public void ComputeCommandIdempotencyKey_ShouldReturnDifferentKeys_ForDifferentCommandTypes()
    {
        // Arrange
        var playerId = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        // Act
        var key1 = _sut.ComputeCommandIdempotencyKey(
            gameId,
            playerId,
            typeof(DeployUnitCommand),
            1, 
            nameof(PhaseNames.Deployment),
            unitId);
        var key2 = _sut.ComputeCommandIdempotencyKey(
            gameId,
            playerId,
            typeof(MoveUnitCommand),
            1, 
            nameof(PhaseNames.Deployment),
            unitId);

        // Assert
        key1.ShouldNotBe(key2);
    }

    [Fact]
    public void ComputeCommandIdempotencyKey_ShouldReturnDifferentKeys_ForDifferentPhases()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        var commandType = typeof(DeployUnitCommand);

        // Get key in current phase
        var key1 = _sut.ComputeCommandIdempotencyKey(
            gameId,
            playerId,
            commandType,
            1, 
            nameof(PhaseNames.Deployment),
            unitId);

        // Get key in new phase
        var key2 = _sut.ComputeCommandIdempotencyKey(
            gameId,
            playerId,
            commandType,
            1, 
            nameof(PhaseNames.Movement),
            unitId);

        // Assert
        key1.ShouldNotBe(key2);
    }
    
    [Fact]
    public void ComputeCommandIdempotencyKey_ShouldReturnDifferentKeys_ForDifferentTurns()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        var commandType = typeof(DeployUnitCommand);

        // Act
        var key1 = _sut.ComputeCommandIdempotencyKey(
            gameId, playerId, commandType, 1, nameof(PhaseNames.Deployment), unitId);
        var key2 = _sut.ComputeCommandIdempotencyKey(
            gameId, playerId, commandType, 2, nameof(PhaseNames.Deployment), unitId);

        // Assert
        key1.ShouldNotBe(key2);
    }

    [Fact]
    public void ComputeCommandIdempotencyKey_ShouldReturnDifferentKeys_ForDifferentUnits()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var unitId1 = Guid.NewGuid();
        var unitId2 = Guid.NewGuid();
        var commandType = typeof(DeployUnitCommand);

        // Act
        var key1 = _sut.ComputeCommandIdempotencyKey(
            gameId, playerId, commandType, 1, nameof(PhaseNames.Deployment), unitId1);
        var key2 = _sut.ComputeCommandIdempotencyKey(
            gameId, playerId, commandType, 1, nameof(PhaseNames.Deployment), unitId2);

        // Assert
        key1.ShouldNotBe(key2);
    }
}