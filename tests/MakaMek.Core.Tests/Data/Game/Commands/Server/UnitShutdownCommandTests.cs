using NSubstitute;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Pilots;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.Tests.Utils;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Map.Models;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Data.Game.Commands.Server;

public class UnitShutdownCommandTests
{
    private readonly FakeLocalizationService _localizationService = new();
    private readonly IGame _game = Substitute.For<IGame>();
    private readonly Guid _gameId = Guid.NewGuid();
    private readonly Unit _unit;

    public UnitShutdownCommandTests()
    {
        var player = new Player(Guid.NewGuid(), "Player 1", PlayerControlType.Human);

        // Create unit using MechFactory
        var mechFactory = new MechFactory(
            new ClassicBattletechRulesProvider(),
            new ClassicBattletechComponentProvider(),
            _localizationService);
        var unitData = MechFactoryTests.CreateDummyMechData();
        unitData.Id = Guid.NewGuid();
        
        _unit = mechFactory.Create(unitData);
        
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
        result.ShouldContain("LCT-1V avoided shutdown (heat level 15)");
        result.ShouldContain("Avoid Number: 8");
        result.ShouldContain("Roll Result: 11");
    }

    [Fact]
    public void Render_AutomaticHeatShutdown_ShouldFormatCorrectly()
    {
        // Arrange
        // Set unit heat for testing
        _unit.ApplyHeat(new HeatData
        {
            MovementHeatSources = [
                new MovementHeatData
                {
                    MovementType = MovementType.Run,
                    MovementPointsSpent = 5,
                    HeatPoints = 30
                }
            ],
            WeaponHeatSources = [],
            ExternalHeatSources = [],
            DissipationData = default
        });
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
        result.ShouldBe("LCT-1V automatically shut down due to excessive heat (level 30)");
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
        result.ShouldBe("LCT-1V shut down due to unconscious pilot (heat level 0)");
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
        result.ShouldContain("LCT-1V shut down due to heat (level 20)");
        result.ShouldContain("Avoid Number: 10");
        result.ShouldContain("Roll Result: 5");
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
        result.ShouldBe("LCT-1V voluntarily shut down");
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
        result.ShouldBe("LCT-1V shut down");
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
