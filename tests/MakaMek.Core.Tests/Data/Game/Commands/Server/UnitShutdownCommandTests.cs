using NSubstitute;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Pilots;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.Tests.Data.Community;
using Sanet.MakaMek.Core.Utils.TechRules;
using Sanet.MakaMek.Core.Utils;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Data.Game.Commands.Server;

public class UnitShutdownCommandTests
{
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();
    private readonly IGame _game = Substitute.For<IGame>();
    private readonly Guid _gameId = Guid.NewGuid();
    private readonly Unit _unit;

    public UnitShutdownCommandTests()
    {
        var player = new Player(Guid.NewGuid(), "Player 1");

        // Create unit using MechFactory
        var mechFactory = new MechFactory(new ClassicBattletechRulesProvider(), _localizationService);
        var unitData = MechFactoryTests.CreateDummyMechData();
        unitData.Id = Guid.NewGuid();
        
        _unit = mechFactory.Create(unitData);
        
        // Add unit to player
        player.AddUnit(_unit);
        
        // Setup game to return players
        _game.Players.Returns(new List<IPlayer> { player });
        
        // Setup localization service
        _localizationService.GetString("Command_MechShutdown_Avoided")
            .Returns("{0} avoided shutdown (heat level {1}, roll: [{2}] = {3} vs {4})");
        _localizationService.GetString("Command_MechShutdown_AutomaticHeat")
            .Returns("{0} automatically shut down due to excessive heat (level {1})");
        _localizationService.GetString("Command_MechShutdown_UnconsciousPilot")
            .Returns("{0} shut down due to unconscious pilot (heat level {1})");
        _localizationService.GetString("Command_MechShutdown_FailedRoll")
            .Returns("{0} shut down due to heat (level {1}, roll: [{2}] = {3} vs {4})");
        _localizationService.GetString("Command_MechShutdown_Voluntary")
            .Returns("{0} voluntarily shut down");
        _localizationService.GetString("Command_MechShutdown_Generic")
            .Returns("{0} shut down");
    }

    private AvoidShutdownRollData CreateSuccessfulRollData()
    {
        return new AvoidShutdownRollData
        {
            HeatLevel = 15,
            DiceResults = [5, 6],
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
    public void Render_AvoidedShutdown_ShouldFormatCorrectly()
    {
        // Arrange
        var command = new UnitShutdownCommand
        {
            GameOriginId = _gameId,
            UnitId = _unit.Id,
            ShutdownData = new ShutdownData { Reason = ShutdownReason.Heat, Turn = 1 },
            AvoidShutdownRoll = CreateSuccessfulRollData(),
            IsAutomaticShutdown = false,
            Timestamp = DateTime.UtcNow
        };

        // Act
        var result = command.Render(_localizationService, _game);

        // Assert
        result.ShouldBe("Locust LCT-1V avoided shutdown (heat level 15, roll: [5, 6] = 11 vs 8)");
    }

    [Fact]
    public void Render_AutomaticHeatShutdown_ShouldFormatCorrectly()
    {
        // Arrange
        // Set unit heat for testing
        _unit.GetType().GetProperty("CurrentHeat")?.SetValue(_unit, 30);
        var command = new UnitShutdownCommand
        {
            GameOriginId = _gameId,
            UnitId = _unit.Id,
            ShutdownData = new ShutdownData { Reason = ShutdownReason.Heat, Turn = 1 },
            AvoidShutdownRoll = null,
            IsAutomaticShutdown = true,
            Timestamp = DateTime.UtcNow
        };

        // Act
        var result = command.Render(_localizationService, _game);

        // Assert
        result.ShouldBe("Locust LCT-1V automatically shut down due to excessive heat (level 30)");
    }

    [Fact]
    public void Render_UnconsciousPilotShutdown_ShouldFormatCorrectly()
    {
        // Arrange
        var pilot = Substitute.For<IPilot>();
        pilot.IsConscious.Returns(false);
        _unit.AssignPilot(pilot);
        
        var command = new UnitShutdownCommand
        {
            GameOriginId = _gameId,
            UnitId = _unit.Id,
            ShutdownData = new ShutdownData { Reason = ShutdownReason.Heat, Turn = 1 },
            AvoidShutdownRoll = null,
            IsAutomaticShutdown = true,
            Timestamp = DateTime.UtcNow
        };

        // Act
        var result = command.Render(_localizationService, _game);

        // Assert
        result.ShouldBe("Locust LCT-1V shut down due to unconscious pilot (heat level 0)");
    }

    [Fact]
    public void Render_FailedRollShutdown_ShouldFormatCorrectly()
    {
        // Arrange
        var command = new UnitShutdownCommand
        {
            GameOriginId = _gameId,
            UnitId = _unit.Id,
            ShutdownData = new ShutdownData { Reason = ShutdownReason.Heat, Turn = 1 },
            AvoidShutdownRoll = CreateFailedRollData(),
            IsAutomaticShutdown = false,
            Timestamp = DateTime.UtcNow
        };

        // Act
        var result = command.Render(_localizationService, _game);

        // Assert
        result.ShouldBe("Locust LCT-1V shut down due to heat (level 20, roll: [2, 3] = 5 vs 10)");
    }

    [Fact]
    public void Render_VoluntaryShutdown_ShouldFormatCorrectly()
    {
        // Arrange
        var command = new UnitShutdownCommand
        {
            GameOriginId = _gameId,
            UnitId = _unit.Id,
            ShutdownData = new ShutdownData { Reason = ShutdownReason.Voluntary, Turn = 1 },
            AvoidShutdownRoll = null,
            IsAutomaticShutdown = false,
            Timestamp = DateTime.UtcNow
        };

        // Act
        var result = command.Render(_localizationService, _game);

        // Assert
        result.ShouldBe("Locust LCT-1V voluntarily shut down");
    }

    [Fact]
    public void Render_GenericShutdown_ShouldFormatCorrectly()
    {
        // Arrange - create a command that doesn't match any specific pattern
        var command = new UnitShutdownCommand
        {
            GameOriginId = _gameId,
            UnitId = _unit.Id,
            ShutdownData = new ShutdownData { Reason = (ShutdownReason)99, Turn = 1 }, // Unknown reason
            AvoidShutdownRoll = null,
            IsAutomaticShutdown = false,
            Timestamp = DateTime.UtcNow
        };

        // Act
        var result = command.Render(_localizationService, _game);

        // Assert
        result.ShouldBe("Locust LCT-1V shut down");
    }

    [Fact]
    public void Render_UnitNotFound_ShouldReturnEmpty()
    {
        // Arrange
        var command = new UnitShutdownCommand
        {
            GameOriginId = _gameId,
            UnitId = Guid.NewGuid(), // Different unit ID that doesn't exist
            ShutdownData = new ShutdownData { Reason = ShutdownReason.Heat, Turn = 1 },
            AvoidShutdownRoll = CreateFailedRollData(),
            IsAutomaticShutdown = false,
            Timestamp = DateTime.UtcNow
        };

        // Act
        var result = command.Render(_localizationService, _game);

        // Assert
        result.ShouldBeEmpty();
    }
}
