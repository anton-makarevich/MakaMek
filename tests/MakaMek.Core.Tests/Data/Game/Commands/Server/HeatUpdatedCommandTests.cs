using NSubstitute;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.Tests.Data.Community;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Core.Utils.TechRules;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Data.Game.Commands.Server;

public class HeatUpdatedCommandTests
{
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();
    private readonly IGame _game = Substitute.For<IGame>();
    private readonly Guid _gameId = Guid.NewGuid();
    private readonly Unit _unit;

    public HeatUpdatedCommandTests()
    {
        var player =
            // Create player
            new Player(Guid.NewGuid(), "Player 1");

        // Create a unit using MechFactory
        var mechFactory = new MechFactory(new ClassicBattletechRulesProvider(),_localizationService);
        var mechData = MechFactoryTests.CreateDummyMechData();
        mechData.Id = Guid.NewGuid();
        
        _unit = mechFactory.Create(mechData);
        
        // Add unit to player
        player.AddUnit(_unit);
        
        // Setup game to return players
        _game.Players.Returns(new List<IPlayer> { player });
        
        // Setup localization service
        _localizationService.GetString("Command_HeatUpdated_Header")
            .Returns("Heat update for {0} (Previous: {1})");
        _localizationService.GetString("Command_HeatUpdated_Sources")
            .Returns("Heat sources:");
        _localizationService.GetString("Command_HeatUpdated_MovementHeat")
            .Returns("  + {0} movement ({1} MP): {2} heat");
        _localizationService.GetString("Command_HeatUpdated_WeaponHeat")
            .Returns("  + Firing {0}: {1} heat");
        _localizationService.GetString("Command_HeatUpdated_TotalGenerated")
            .Returns("Total heat generated: {0}");
        _localizationService.GetString("Command_HeatUpdated_Dissipation")
            .Returns("  - Heat dissipation from {0} heat sinks and {1} engine heat sinks: -{2} heat");
    }

    [Fact]
    public void Render_WithNoHeatSources_ReturnsExpectedString()
    {
        // Arrange
        var heatData = new HeatData
        {
            MovementHeatSources = [],
            WeaponHeatSources = [],
            DissipationData = new HeatDissipationData
            {
                HeatSinks = 10,
                EngineHeatSinks = 10,
                DissipationPoints = 20
            }
        };

        var command = new HeatUpdatedCommand
        {
            UnitId = _unit.Id,
            HeatData = heatData,
            PreviousHeat = 0,
            GameOriginId = _gameId,
            Timestamp = DateTime.UtcNow
        };

        // Act
        var result = command.Render(_localizationService, _game);

        // Assert
        result.ShouldContain($"Heat update for {_unit.Name} (Previous: 0)");
        result.ShouldContain("Heat sources:");
        result.ShouldContain("Total heat generated: 0");
        result.ShouldContain("Heat dissipation from 10 heat sinks and 10 engine heat sinks: -20 heat");
    }

    [Fact]
    public void Render_WithMovementHeat_ReturnsExpectedString()
    {
        // Arrange
        var movementHeatSources = new List<MovementHeatData>
        {
            new() { MovementType = MovementType.Run, MovementPointsSpent = 5, HeatPoints = 2 }
        };

        var heatData = new HeatData
        {
            MovementHeatSources = movementHeatSources,
            WeaponHeatSources = [],
            DissipationData = new HeatDissipationData
            {
                HeatSinks = 10,
                EngineHeatSinks = 10,
                DissipationPoints = 20
            }
        };

        var command = new HeatUpdatedCommand
        {
            UnitId = _unit.Id,
            HeatData = heatData,
            PreviousHeat = 0,
            GameOriginId = _gameId,
            Timestamp = DateTime.UtcNow
        };

        // Act
        var result = command.Render(_localizationService, _game);

        // Assert
        result.ShouldContain($"Heat update for {_unit.Name} (Previous: 0)");
        result.ShouldContain("Heat sources:");
        result.ShouldContain("Run movement (5 MP): 2 heat");
        result.ShouldContain("Total heat generated: 2");
        result.ShouldContain("Heat dissipation from 10 heat sinks and 10 engine heat sinks: -20 heat");
    }

    [Fact]
    public void Render_WithWeaponHeat_ReturnsExpectedString()
    {
        // Arrange
        var weaponHeatSources = new List<WeaponHeatData>
        {
            new() { WeaponName = "Medium Laser", HeatPoints = 3 },
            new() { WeaponName = "Large Laser", HeatPoints = 8 }
        };

        var heatData = new HeatData
        {
            MovementHeatSources = [],
            WeaponHeatSources = weaponHeatSources,
            DissipationData = new HeatDissipationData
            {
                HeatSinks = 10,
                EngineHeatSinks = 10,
                DissipationPoints = 20
            }
        };

        var command = new HeatUpdatedCommand
        {
            UnitId = _unit.Id,
            HeatData = heatData,
            PreviousHeat = 0,
            GameOriginId = _gameId,
            Timestamp = DateTime.UtcNow
        };

        // Act
        var result = command.Render(_localizationService, _game);

        // Assert
        result.ShouldContain($"Heat update for {_unit.Name} (Previous: 0)");
        result.ShouldContain("Heat sources:");
        result.ShouldContain("Firing Medium Laser: 3 heat");
        result.ShouldContain("Firing Large Laser: 8 heat");
        result.ShouldContain("Total heat generated: 11");
        result.ShouldContain("Heat dissipation from 10 heat sinks and 10 engine heat sinks: -20 heat");
    }

    [Fact]
    public void Render_WithCombinedHeatSources_ReturnsExpectedString()
    {
        // Arrange
        var movementHeatSources = new List<MovementHeatData>
        {
            new() { MovementType = MovementType.Jump, MovementPointsSpent = 3, HeatPoints = 3 }
        };

        var weaponHeatSources = new List<WeaponHeatData>
        {
            new() { WeaponName = "Medium Laser", HeatPoints = 3 },
            new() { WeaponName = "PPC", HeatPoints = 10 }
        };

        var heatData = new HeatData
        {
            MovementHeatSources = movementHeatSources,
            WeaponHeatSources = weaponHeatSources,
            DissipationData = new HeatDissipationData
            {
                HeatSinks = 10,
                EngineHeatSinks = 10,
                DissipationPoints = 20
            }
        };

        var command = new HeatUpdatedCommand
        {
            UnitId = _unit.Id,
            HeatData = heatData,
            PreviousHeat = 5,
            GameOriginId = _gameId,
            Timestamp = DateTime.UtcNow
        };

        // Act
        var result = command.Render(_localizationService, _game);

        // Assert
        result.ShouldContain($"Heat update for {_unit.Name} (Previous: 5)");
        result.ShouldContain("Heat sources:");
        result.ShouldContain("Jump movement (3 MP): 3 heat");
        result.ShouldContain("Firing Medium Laser: 3 heat");
        result.ShouldContain("Firing PPC: 10 heat");
        result.ShouldContain("Total heat generated: 16");
        result.ShouldContain("Heat dissipation from 10 heat sinks and 10 engine heat sinks: -20 heat");
    }

    [Fact]
    public void Render_WithUnitNotFound_ReturnsEmptyString()
    {
        // Arrange
        var heatData = new HeatData
        {
            MovementHeatSources = [],
            WeaponHeatSources = [],
            DissipationData = new HeatDissipationData
            {
                HeatSinks = 10,
                EngineHeatSinks = 10,
                DissipationPoints = 20
            }
        };

        var command = new HeatUpdatedCommand
        {
            UnitId = Guid.NewGuid(), // Different unit ID that doesn't exist in the game
            HeatData = heatData,
            PreviousHeat = 0,
            GameOriginId = _gameId,
            Timestamp = DateTime.UtcNow
        };

        // Act
        var result = command.Render(_localizationService, _game);

        // Assert
        result.ShouldBeEmpty();
    }
}
