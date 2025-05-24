using Shouldly;
using NSubstitute;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Commands.Server;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.Tests.Data.Community;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Core.Utils.TechRules;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Commands.Server;

public class MechFallingCommandTests
{
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();
    private readonly IGame _game = Substitute.For<IGame>();
    private readonly Guid _gameId = Guid.NewGuid();
    private readonly Unit _unit;

    public MechFallingCommandTests()
    {
        var player =
            // Create player
            new Player(Guid.NewGuid(), "Player 1");

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
        _localizationService.GetString("Command_MechFalling_Base")
            .Returns("{0} fell");
        _localizationService.GetString("Command_MechFalling_Levels")
            .Returns(" {0} level(s)");
        _localizationService.GetString("Command_MechFalling_Jumping")
            .Returns(" while jumping");
        _localizationService.GetString("Command_MechFalling_Damage")
            .Returns(" and took {0} damage");
        _localizationService.GetString("Command_MechFalling_PilotInjury")
            .Returns(", pilot was injured");
    }

    private MechFallingCommand CreateBasicFallingCommand()
    {
        var hitLocations = new List<HitLocationData>
        {
            new(PartLocation.CenterTorso, 5, [new DiceResult(6)])
        };
        
        var hitLocationsData = new HitLocationsData(
            hitLocations,
            5);
        
        var fallingDamageData = new FallingDamageData(
            HexDirection.Top,
            hitLocationsData,
            new DiceResult(1));

        return new MechFallingCommand
        {
            GameOriginId = _gameId,
            UnitId = _unit.Id,
            LevelsFallen = 0,
            WasJumping = false,
            DamageData = fallingDamageData,
            Timestamp = DateTime.UtcNow
        };
    }

    private MechFallingCommand CreateFallingWithLevelsCommand()
    {
        var command = CreateBasicFallingCommand();
        return command with { LevelsFallen = 2 };
    }

    private MechFallingCommand CreateJumpingFallCommand()
    {
        var command = CreateBasicFallingCommand();
        return command with { WasJumping = true };
    }

    private MechFallingCommand CreatePilotInjuryCommand()
    {
        var hitLocations = new List<HitLocationData>
        {
            new(PartLocation.CenterTorso, 5, [new DiceResult(6)])
        };
        
        var hitLocationsData = new HitLocationsData(
            hitLocations,
            5);
        
        var fallingDamageData = new FallingDamageData(
            HexDirection.Top,
            hitLocationsData,
            new DiceResult(1),
            true,
            [new DiceResult(6)]);

        return new MechFallingCommand
        {
            GameOriginId = _gameId,
            UnitId = _unit.Id,
            LevelsFallen = 0,
            WasJumping = false,
            DamageData = fallingDamageData,
            Timestamp = DateTime.UtcNow
        };
    }

    private MechFallingCommand CreateComplexFallCommand()
    {
        var hitLocations = new List<HitLocationData>
        {
            new(PartLocation.CenterTorso, 5, [new DiceResult(6)]),
            new(PartLocation.LeftLeg, 3, [new DiceResult(4)])
        };
        
        var hitLocationsData = new HitLocationsData(
            hitLocations,
            8);
        
        var fallingDamageData = new FallingDamageData(
            HexDirection.Top,
            hitLocationsData,
            new DiceResult(1),
            true,
            [new DiceResult(6)]);

        return new MechFallingCommand
        {
            GameOriginId = _gameId,
            UnitId = _unit.Id,
            LevelsFallen = 2,
            WasJumping = true,
            DamageData = fallingDamageData,
            Timestamp = DateTime.UtcNow
        };
    }

    [Fact]
    public void Format_ShouldFormatBasicFall_Correctly()
    {
        // Arrange
        var command = CreateBasicFallingCommand();

        // Act
        var result = command.Format(_localizationService, _game);

        // Assert
        result.ShouldNotBeEmpty();
        result.ShouldContain("Locust LCT-1V fell");
        result.ShouldContain("and took 5 damage");
        result.ShouldNotContain("level(s)");
        result.ShouldNotContain("while jumping");
        result.ShouldNotContain("pilot was injured");
    }

    [Fact]
    public void Format_ShouldIncludeLevelsFallen_WhenApplicable()
    {
        // Arrange
        var command = CreateFallingWithLevelsCommand();

        // Act
        var result = command.Format(_localizationService, _game);

        // Assert
        result.ShouldNotBeEmpty();
        result.ShouldContain("Locust LCT-1V fell");
        result.ShouldContain("2 level(s)");
        result.ShouldContain("and took 5 damage");
    }

    [Fact]
    public void Format_ShouldIncludeJumpingStatus_WhenApplicable()
    {
        // Arrange
        var command = CreateJumpingFallCommand();

        // Act
        var result = command.Format(_localizationService, _game);

        // Assert
        result.ShouldNotBeEmpty();
        result.ShouldContain("Locust LCT-1V fell");
        result.ShouldContain("while jumping");
        result.ShouldContain("and took 5 damage");
    }

    [Fact]
    public void Format_ShouldIncludePilotInjury_WhenApplicable()
    {
        // Arrange
        var command = CreatePilotInjuryCommand();

        // Act
        var result = command.Format(_localizationService, _game);

        // Assert
        result.ShouldNotBeEmpty();
        result.ShouldContain("Locust LCT-1V fell");
        result.ShouldContain("and took 5 damage");
        result.ShouldContain("pilot was injured");
    }

    [Fact]
    public void Format_ShouldIncludeAllElements_ForComplexFall()
    {
        // Arrange
        var command = CreateComplexFallCommand();

        // Act
        var result = command.Format(_localizationService, _game);

        // Assert
        result.ShouldNotBeEmpty();
        result.ShouldContain("Locust LCT-1V fell");
        result.ShouldContain("2 level(s)");
        result.ShouldContain("while jumping");
        result.ShouldContain("and took 8 damage");
        result.ShouldContain("pilot was injured");
    }

    [Fact]
    public void Format_ShouldReturnEmpty_WhenUnitNotFound()
    {
        // Arrange
        var command = CreateBasicFallingCommand() with { UnitId = Guid.NewGuid() };

        // Act
        var result = command.Format(_localizationService, _game);

        // Assert
        result.ShouldBeEmpty();
    }
}
