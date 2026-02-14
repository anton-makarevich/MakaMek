using Sanet.MakaMek.Bots.Data;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Models;
using Shouldly;

namespace Sanet.MakaMek.Bots.Tests.Models;

/// <summary>
/// Unit tests for TurnState functionality
/// </summary>
public class TurnStateTests
{
    private readonly Guid _gameId = Guid.NewGuid();
    private const int TurnNumber = 5;
    private readonly Bots.Models.TurnState _turnState;

    public TurnStateTests()
    {
        _turnState = new Bots.Models.TurnState(_gameId, TurnNumber);
    }

    [Fact]
    public void Constructor_ShouldSetPropertiesCorrectly()
    {
        // Assert
        _turnState.GameId.ShouldBe(_gameId);
        _turnState.TurnNumber.ShouldBe(TurnNumber);
    }

    [Fact]
    public void TryGetTargetEvaluation_WhenKeyNotExists_ShouldReturnFalse()
    {
        // Arrange
        var key = new TargetEvaluationKey(
            Guid.NewGuid(), 
            new HexCoordinates(1, 1), 
            HexDirection.Top,
            MovementType.Walk,
            Guid.NewGuid(),
            new HexCoordinates(2, 2),
            HexDirection.Bottom);

        // Act
        var result = _turnState.TryGetTargetEvaluation(key, out _);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void AddTargetEvaluation_ThenTryGet_ShouldReturnTrueAndCorrectData()
    {
        // Arrange
        var key = new TargetEvaluationKey(
            Guid.NewGuid(), 
            new HexCoordinates(1, 1), 
            HexDirection.Top,
            MovementType.Walk,
            Guid.NewGuid(),
            new HexCoordinates(2, 2),
            HexDirection.Bottom);

        var expectedData = new TargetEvaluationData
        {
            TargetId = key.TargetId,
            ConfigurationScores = new List<WeaponConfigurationEvaluationData>
            {
                new()
                {
                    Configuration = new WeaponConfiguration
                    {
                        Type = WeaponConfigurationType.None,Value = 0
                    },
                    Score = 100.0,
                    ViableWeapons = new List<WeaponEvaluationData>()
                }
            }
        };

        // Act
        _turnState.AddTargetEvaluation(key, expectedData);
        var result = _turnState.TryGetTargetEvaluation(key, out var actualData);

        // Assert
        result.ShouldBeTrue();
        actualData.TargetId.ShouldBe(key.TargetId);
        actualData.ConfigurationScores.Count.ShouldBe(1);
        actualData.ConfigurationScores[0].Score.ShouldBe(100.0);
    }

    [Fact]
    public void TryGetTargetEvaluation_WithDifferentKey_ShouldReturnFalse()
    {
        // Arrange
        var key1 = new TargetEvaluationKey(
            Guid.NewGuid(), 
            new HexCoordinates(1, 1), 
            HexDirection.Top,
            MovementType.Walk,
            Guid.NewGuid(),
            new HexCoordinates(2, 2),
            HexDirection.Bottom);

        var key2 = new TargetEvaluationKey(
            Guid.NewGuid(), 
            new HexCoordinates(1, 1), 
            HexDirection.Top,
            MovementType.Walk,
            Guid.NewGuid(),
            new HexCoordinates(2, 2),
            HexDirection.Bottom);

        var data = new TargetEvaluationData
        {
            TargetId = key1.TargetId,
            ConfigurationScores = []
        };

        // Act
        _turnState.AddTargetEvaluation(key1, data);
        var result = _turnState.TryGetTargetEvaluation(key2, out _);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void AddTargetEvaluation_WithSameKey_ShouldNotOverwrite()
    {
        // Arrange
        var key = new TargetEvaluationKey(
            Guid.NewGuid(),
            new HexCoordinates(1, 1),
            HexDirection.Top,
            MovementType.Walk,
            Guid.NewGuid(),
            new HexCoordinates(2, 2),
            HexDirection.Bottom);

        var data1 = new TargetEvaluationData
        {
            TargetId = key.TargetId,
            ConfigurationScores = []
        };

        var data2 = new TargetEvaluationData
        {
            TargetId = Guid.NewGuid(),
            ConfigurationScores = []
        };

        // Act
        _turnState.AddTargetEvaluation(key, data1);
        _turnState.AddTargetEvaluation(key, data2); // Try to add with the same key
        var getResult = _turnState.TryGetTargetEvaluation(key, out var retrievedData);

        // Assert
       getResult.ShouldBeTrue();
       retrievedData.ShouldBe(data1); // Should return the first data
    }

    [Fact]
    public void TargetEvaluationKey_Equality_ShouldWorkCorrectly()
    {
        // Arrange
        var key1 = new TargetEvaluationKey(
            Guid.NewGuid(), 
            new HexCoordinates(1, 1), 
            HexDirection.Top,
            MovementType.Walk,
            Guid.NewGuid(),
            new HexCoordinates(2, 2),
            HexDirection.Bottom);

        var key2 = new TargetEvaluationKey(
            key1.AttackerId, 
            key1.AttackerCoords, 
            key1.AttackerFacing,
            key1.AttackerMovementType,
            key1.TargetId,
            key1.TargetCoords,
            key1.TargetFacing);

        var key3 = key1 with { AttackerId = Guid.NewGuid() };

        // Act & Assert
        key1.ShouldBe(key2); // The same values should be equal
        key1.ShouldNotBe(key3); // Different attacker ID should not be equal
    }

    [Fact]
    public void TargetEvaluationKey_DifferentCoordinates_ShouldNotBeEqual()
    {
        // Arrange
        var key1 = new TargetEvaluationKey(
            Guid.NewGuid(), 
            new HexCoordinates(1, 1), 
            HexDirection.Top,
            MovementType.Walk,
            Guid.NewGuid(),
            new HexCoordinates(2, 2),
            HexDirection.Bottom);

        var key2 = key1 with { AttackerCoords = new HexCoordinates(1, 2) };

        // Act & Assert
        key1.ShouldNotBe(key2);
    }

    [Fact]
    public void TargetEvaluationKey_DifferentFacing_ShouldNotBeEqual()
    {
        // Arrange
        var key1 = new TargetEvaluationKey(
            Guid.NewGuid(), 
            new HexCoordinates(1, 1), 
            HexDirection.Top,
            MovementType.Walk,
            Guid.NewGuid(),
            new HexCoordinates(2, 2),
            HexDirection.Bottom);

        var key2 = key1 with { AttackerFacing = HexDirection.Bottom };

        // Act & Assert
        key1.ShouldNotBe(key2);
    }
    
    [Fact]
    public void TargetEvaluationKey_DifferentMovementType_ShouldNotBeEqual()
    {
        // Arrange
        var key1 = new TargetEvaluationKey(
            Guid.NewGuid(), 
            new HexCoordinates(1, 1), 
            HexDirection.Top,
            MovementType.Walk,
            Guid.NewGuid(),
            new HexCoordinates(2, 2),
            HexDirection.Bottom);

        var key2 = key1 with { AttackerMovementType = MovementType.Run };

        // Act & Assert
        key1.ShouldNotBe(key2);
    }
}
