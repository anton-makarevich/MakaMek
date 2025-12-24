using NSubstitute;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.Tests.Utils;
using Sanet.MakaMek.Core.Utils;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Data.Game.Commands.Server;

public class UnitStartupCommandTests
{
    private readonly FakeLocalizationService _localizationService = new();
    private readonly IGame _game = Substitute.For<IGame>();
    private readonly Guid _gameId = Guid.NewGuid();
    private readonly Unit _unit;

    public UnitStartupCommandTests()
    {
        var player = new Player(Guid.NewGuid(), "Player 1", PlayerControlType.Human);

        // Create a unit using MechFactory
        var mechFactory = new MechFactory(
            new ClassicBattletechRulesProvider(),
            new ClassicBattletechComponentProvider(),
            _localizationService);
        var unitData = MechFactoryTests.CreateDummyMechData();
        unitData.Id = Guid.NewGuid();
        
        _unit = mechFactory.Create(unitData);
        
        // Set unit heat for testing
        _unit.ApplyHeat(new HeatData
        {
            MovementHeatSources = [
                new MovementHeatData
                {
                    MovementType = MovementType.Run,
                    MovementPointsSpent = 5,
                    HeatPoints = 15
                }
            ],
            WeaponHeatSources = [],
            ExternalHeatSources = [],
            DissipationData = default
        });
        
        // Add unit to player
        player.AddUnit(_unit);
        
        // Setup game to return players
        _game.Players.Returns(new List<IPlayer> { player });
    }

    private AvoidShutdownRollData CreateSuccessfulRollData()
    {
        return new AvoidShutdownRollData
        {
            HeatLevel = 15,
            DiceResults = [4, 5],
            AvoidNumber = 8,
            IsSuccessful = true
        };
    }

    private AvoidShutdownRollData CreateFailedRollData()
    {
        return new AvoidShutdownRollData
        {
            HeatLevel = 20,
            DiceResults = [2, 3],
            AvoidNumber = 10,
            IsSuccessful = false
        };
    }

    [Fact]
    public void Render_AutomaticRestart_ShouldFormatCorrectly()
    {
        // Arrange
        var command = new UnitStartupCommand
        {
            GameOriginId = _gameId,
            UnitId = _unit.Id,
            IsAutomaticRestart = true,
            IsRestartPossible = true,
            AvoidShutdownRoll = null,
            Timestamp = DateTime.UtcNow
        };

        // Act
        var result = command.Render(_localizationService, _game);

        // Assert
        result.ShouldBe("LCT-1V automatically restarted (heat level 15)");
    }

    [Fact]
    public void Render_ImpossibleRestart_ShouldFormatCorrectly()
    {
        // Arrange
        var command = new UnitStartupCommand
        {
            GameOriginId = _gameId,
            UnitId = _unit.Id,
            IsAutomaticRestart = false,
            IsRestartPossible = false,
            AvoidShutdownRoll = null,
            Timestamp = DateTime.UtcNow
        };

        // Act
        var result = command.Render(_localizationService, _game);

        // Assert
        result.ShouldBe("LCT-1V cannot restart (heat level 15)");
    }

    [Fact]
    public void Render_SuccessfulRestart_ShouldFormatCorrectly()
    {
        // Arrange
        var command = new UnitStartupCommand
        {
            GameOriginId = _gameId,
            UnitId = _unit.Id,
            IsAutomaticRestart = false,
            IsRestartPossible = true,
            AvoidShutdownRoll = CreateSuccessfulRollData(),
            Timestamp = DateTime.UtcNow
        };

        // Act
        var result = command.Render(_localizationService, _game);

        // Assert
        result.ShouldContain("LCT-1V successfully restarted (heat level 15)");
        result.ShouldContain("Avoid Number: 8");
        result.ShouldContain("Roll Result: 9");
    }

    [Fact]
    public void Render_FailedRestart_ShouldFormatCorrectly()
    {
        // Arrange
        var command = new UnitStartupCommand
        {
            GameOriginId = _gameId,
            UnitId = _unit.Id,
            IsAutomaticRestart = false,
            IsRestartPossible = true,
            AvoidShutdownRoll = CreateFailedRollData(),
            Timestamp = DateTime.UtcNow
        };

        // Act
        var result = command.Render(_localizationService, _game);

        // Assert
        result.ShouldContain("LCT-1V failed to restart (heat level 20)");
        result.ShouldContain("Avoid Number: 10");
        result.ShouldContain("Roll Result: 5");
    }

    [Fact]
    public void Render_GenericCase_ShouldFormatCorrectly()
    {
        // Arrange - create a command that doesn't match any specific pattern
        var command = new UnitStartupCommand
        {
            GameOriginId = _gameId,
            UnitId = _unit.Id,
            IsAutomaticRestart = false,
            IsRestartPossible = true,
            AvoidShutdownRoll = null, // This creates the generic case
            Timestamp = DateTime.UtcNow
        };

        // Act
        var result = command.Render(_localizationService, _game);

        // Assert
        result.ShouldBe("LCT-1V restart attempt");
    }

    [Fact]
    public void Render_UnitNotFound_ShouldReturnEmpty()
    {
        // Arrange
        var command = new UnitStartupCommand
        {
            GameOriginId = _gameId,
            UnitId = Guid.NewGuid(), // Different unit ID that doesn't exist
            IsAutomaticRestart = true,
            IsRestartPossible = true,
            AvoidShutdownRoll = null,
            Timestamp = DateTime.UtcNow
        };

        // Act
        var result = command.Render(_localizationService, _game);

        // Assert
        result.ShouldBeEmpty();
    }
}
