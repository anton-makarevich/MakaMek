using NSubstitute;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.Tests.Data.Community;
using Sanet.MakaMek.Core.Utils;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Data.Game.Commands.Server;

public class UnitStartupCommandTests
{
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();
    private readonly IGame _game = Substitute.For<IGame>();
    private readonly Guid _gameId = Guid.NewGuid();
    private readonly Unit _unit;

    public UnitStartupCommandTests()
    {
        var player = new Player(Guid.NewGuid(), "Player 1");

        // Create unit using MechFactory
        var mechFactory = new MechFactory(new ClassicBattletechRulesProvider(), _localizationService);
        var unitData = MechFactoryTests.CreateDummyMechData();
        unitData.Id = Guid.NewGuid();
        
        _unit = mechFactory.Create(unitData);
        
        // Set unit heat for testing
        _unit.GetType().GetProperty("CurrentHeat")?.SetValue(_unit, 15);
        
        // Add unit to player
        player.AddUnit(_unit);
        
        // Setup game to return players
        _game.Players.Returns(new List<IPlayer> { player });
        
        // Setup localization service
        _localizationService.GetString("Command_MechRestart_Automatic")
            .Returns("{0} automatically restarted (heat level {1})");
        _localizationService.GetString("Command_MechRestart_Successful")
            .Returns("{0} successfully restarted (heat level {1}, roll: [{2}] = {3} vs {4})");
        _localizationService.GetString("Command_MechRestart_Failed")
            .Returns("{0} failed to restart (heat level {1}, roll: [{2}] = {3} vs {4})");
        _localizationService.GetString("Command_MechRestart_Impossible")
            .Returns("{0} cannot restart (heat level {1})");
        _localizationService.GetString("Command_MechRestart_Generic")
            .Returns("{0} restart attempt");
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
        result.ShouldBe("Locust LCT-1V automatically restarted (heat level 15)");
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
        result.ShouldBe("Locust LCT-1V cannot restart (heat level 15)");
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
        result.ShouldBe("Locust LCT-1V successfully restarted (heat level 15, roll: [4, 5] = 9 vs 8)");
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
        result.ShouldBe("Locust LCT-1V failed to restart (heat level 20, roll: [2, 3] = 5 vs 10)");
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
        result.ShouldBe("Locust LCT-1V restart attempt");
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
