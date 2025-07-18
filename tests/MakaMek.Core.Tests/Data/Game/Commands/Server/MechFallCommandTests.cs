using NSubstitute;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.Tests.Data.Community;
using Sanet.MakaMek.Core.Tests.Models.Game.Mechanics.Mechs.Falling;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Core.Utils.TechRules;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Data.Game.Commands.Server;

public class MechFallCommandTests
{
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();
    private readonly IGame _game = Substitute.For<IGame>();
    private readonly Guid _gameId = Guid.NewGuid();
    private readonly Unit _unit;
    private const string PsrDetailsText = "Base Piloting Skill";

    private PilotingSkillRollData CreateTestPsrData(
        bool successful,
        int[] diceResults, 
        PilotingSkillRollType rollType = PilotingSkillRollType.GyroHit)
    {
        return new PilotingSkillRollData
        {
            RollType = rollType,
            DiceResults = diceResults,
            IsSuccessful = successful,
            PsrBreakdown = new PsrBreakdown
            {
                BasePilotingSkill = 4,
                Modifiers = [new FallProcessorTests.TestModifier{Name = "Test Modifier", Value = 1}]
            }
        };
    }

    public MechFallCommandTests()
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
        // PSR rendering 
        _localizationService.GetString("PilotingSkillRollType_GyroHit").Returns("Gyro Hit");
        _localizationService.GetString("PilotingSkillRollType_PilotDamageFromFall").Returns("Pilot Damage From Fall");
        _localizationService.GetString("Command_PilotingSkillRoll_Success").Returns("{0} roll succeeded");
        _localizationService.GetString("Command_PilotingSkillRoll_Failure").Returns("{0} roll failed");
        _localizationService.GetString("Command_PilotingSkillRoll_ImpossibleRoll").Returns("{0} roll is impossible");
        _localizationService.GetString("Command_PilotingSkillRoll_BasePilotingSkill")
            .Returns("Base Piloting Skill: {0}");
        _localizationService.GetString("Command_PilotingSkillRoll_Modifiers").Returns("Modifiers:");
        _localizationService.GetString("Command_PilotingSkillRoll_TotalTargetNumber")
            .Returns("Total Target Number: {0}");
        _localizationService.GetString("Command_PilotingSkillRoll_RollResult").Returns("Roll Result: {0}");
    }

    private FallingDamageData CreateTestFallingDamageData(int totalDamage = 5)
    {
        var hitLocations = new List<HitLocationData>
        {
            new(PartLocation.CenterTorso, totalDamage, [new DiceResult(6)])
        };
        var hitLocationsData = new HitLocationsData(hitLocations, totalDamage);
        return new FallingDamageData(HexDirection.Top, hitLocationsData, new DiceResult(1));
    }

    private MechFallCommand CreateBasicFallingCommand()
    {
        return new MechFallCommand
        {
            GameOriginId = _gameId,
            UnitId = _unit.Id,
            LevelsFallen = 0,
            WasJumping = false,
            DamageData = CreateTestFallingDamageData(),
            Timestamp = DateTime.UtcNow
        };
    }

    private MechFallCommand CreateFallingWithLevelsCommand()
    {
        var sut = CreateBasicFallingCommand();
        return sut with { LevelsFallen = 2 };
    }

    private MechFallCommand CreateJumpingFallCommand()
    {
        var sut = CreateBasicFallingCommand();
        return sut with { WasJumping = true };
    }

    private MechFallCommand CreatePilotInjuryCommand()
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

        return new MechFallCommand
        {
            GameOriginId = _gameId,
            UnitId = _unit.Id,
            LevelsFallen = 0,
            WasJumping = false,
            DamageData = fallingDamageData,
            PilotDamagePilotingSkillRoll = CreateTestPsrData(false, 
                [4, 3],
                PilotingSkillRollType.PilotDamageFromFall),
            Timestamp = DateTime.UtcNow
        };
    }

    private MechFallCommand CreateComplexFallCommand()
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
            new DiceResult(1));

        return new MechFallCommand
        {
            GameOriginId = _gameId,
            UnitId = _unit.Id,
            LevelsFallen = 2,
            WasJumping = true,
            DamageData = fallingDamageData,
            PilotDamagePilotingSkillRoll = CreateTestPsrData(false, 
                [4, 3],
                PilotingSkillRollType.PilotDamageFromFall),
            Timestamp = DateTime.UtcNow
        };
    }

    [Fact]
    public void Render_ShouldFormatBasicFall_Correctly()
    {
        // Arrange
        var sut = CreateBasicFallingCommand();

        // Act
        var result = sut.Render(_localizationService, _game);

        // Assert
        result.ShouldNotBeEmpty();
        result.ShouldContain("Locust LCT-1V fell");
        result.ShouldContain("and took 5 damage");
        result.ShouldNotContain("level(s)");
        result.ShouldNotContain("while jumping");
        result.ShouldNotContain("pilot was injured");
    }

    [Fact]
    public void Render_ShouldIncludeLevelsFallen_WhenApplicable()
    {
        // Arrange
        var sut = CreateFallingWithLevelsCommand();

        // Act
        var result = sut.Render(_localizationService, _game);

        // Assert
        result.ShouldNotBeEmpty();
        result.ShouldContain("Locust LCT-1V fell");
        result.ShouldContain("2 level(s)");
        result.ShouldContain("and took 5 damage");
    }

    [Fact]
    public void Render_ShouldIncludeJumpingStatus_WhenApplicable()
    {
        // Arrange
        var sut = CreateJumpingFallCommand();

        // Act
        var result = sut.Render(_localizationService, _game);

        // Assert
        result.ShouldNotBeEmpty();
        result.ShouldContain("Locust LCT-1V fell");
        result.ShouldContain("while jumping");
        result.ShouldContain("and took 5 damage");
    }

    [Fact]
    public void Render_ShouldIncludePilotInjury_WhenApplicable()
    {
        // Arrange
        var sut = CreatePilotInjuryCommand();

        // Act
        var result = sut.Render(_localizationService, _game);

        // Assert
        result.ShouldNotBeEmpty();
        result.ShouldContain("Locust LCT-1V fell");
        result.ShouldContain("and took 5 damage");
        result.ShouldContain("pilot was injured");
    }

    [Fact]
    public void Render_ShouldIncludeAllElements_ForComplexFall()
    {
        // Arrange
        var sut = CreateComplexFallCommand();
        // Make sure the required string is properly defined
        _localizationService.GetString("Command_WeaponAttackResolution_HitLocations")
            .Returns("Hit Locations:");

        // Act
        var result = sut.Render(_localizationService, _game);

        // Assert
        result.ShouldNotBeEmpty();
        result.ShouldContain("Locust LCT-1V fell");
        result.ShouldContain("2 level(s)");
        result.ShouldContain("while jumping");
        result.ShouldContain("and took 8 damage");
        result.ShouldContain("Hit Locations:");
        foreach (var hitLocation in sut.DamageData!.HitLocations.HitLocations)
        {
            var text = hitLocation.Render(_localizationService, _unit).Trim();
            result.ShouldContain(text);
        }
        result.ShouldContain("pilot was injured");
    }

    [Fact]
    public void Render_ShouldReturnEmpty_WhenUnitNotFound()
    {
        // Arrange
        var sut = CreateBasicFallingCommand() with { UnitId = Guid.NewGuid() };

        // Act
        var result = sut.Render(_localizationService, _game);

        // Assert
        result.ShouldBeEmpty();
    }
    
    [Fact]
    public void Render_ShouldIncludePilotDamagePsr_WhenProvided()
    {
        // Arrange
        var sut = CreatePilotInjuryCommand();

        // Create PSR data
        var pilotDamagePsr = CreateTestPsrData(false,
            [2, 3],PilotingSkillRollType.PilotDamageFromFall);
        
        sut = sut with 
        { 
            PilotDamagePilotingSkillRoll = pilotDamagePsr
        };
        var psrDetailsText = pilotDamagePsr.Render(_localizationService);

        // Act
        var result = sut.Render(_localizationService, _game);

        // Assert
        result.ShouldNotBeEmpty();
        result.ShouldContain("Locust LCT-1V fell");
        result.ShouldContain("pilot was injured");
        result.ShouldContain(psrDetailsText);
    }

    [Fact]
    public void Render_AutoFall_GyroDestroyed_ShouldRenderFallDetails_WithoutFallPsrInfo()
    {
        // Arrange
        var sut = CreateBasicFallingCommand() with
        {
            FallPilotingSkillRoll = null, // Auto-fall, no PSR for the fall itself
            DamageData = CreateTestFallingDamageData(10) // Ensure damage is present
        };

        // Act
        var result = sut.Render(_localizationService, _game);

        // Assert
        result.ShouldBe($"Locust LCT-1V fell and took 10 damage");
        result.ShouldNotContain(PsrDetailsText);
    }

    [Fact]
    public void Render_SuccessfulPsr_GyroHit_ShouldRenderPsrSuccessMessage_AndPsrDetails()
    {
        // Arrange
        var successfulPsr = CreateTestPsrData(true,[4, 3]);
        var sut = new MechFallCommand
        {
            GameOriginId = _gameId,
            UnitId = _unit.Id,
            DamageData = null, // No fall damage
            FallPilotingSkillRoll = successfulPsr,
            Timestamp = DateTime.UtcNow
        };
        var psrDetailsRenderedText = successfulPsr.Render(_localizationService);

        // Act
        var result = sut.Render(_localizationService, _game);

        // Assert
        result.ShouldBe($"{psrDetailsRenderedText}");
    }

    [Fact]
    public void Render_FailedPsr_ResultsInFall_ShouldRenderPsrDetails_AndFallDetails()
    {
        // Arrange
        var failedPsr = CreateTestPsrData(
            false,
            [4, 3]);
        var sut = CreateBasicFallingCommand() with
        {
            FallPilotingSkillRoll = failedPsr,
            DamageData = CreateTestFallingDamageData(7) // Fall damage occurred
        };
        var psrText = failedPsr.Render(_localizationService);

        // Act
        var result = sut.Render(_localizationService, _game);

        // Assert
        result.ShouldContain(psrText); // Expecting PSR details();
        result.ShouldContain("Locust LCT-1V fell and took 7 damage");
    }

    [Fact]
    public void Render_NoDamage_NoFallPsr_ShouldRenderMinimally()
    {
        // Arrange
        var sut = new MechFallCommand
        {
            GameOriginId = _gameId,
            UnitId = _unit.Id,
            DamageData = null,
            FallPilotingSkillRoll = null,
            Timestamp = DateTime.UtcNow
        };

        // Act
        var result = sut.Render(_localizationService, _game);

        // Assert
        result.ShouldBeEmpty(); // Expecting empty if no PSR and no damage
    }

    [Fact]
    public void Render_FallPsrDetails_PilotDamagePsrDetails_AndFallDamage_Correctly()
    {
        // Arrange
        var failedFallPsr = CreateTestPsrData(false, [4, 3]);
        var failedPilotPsr = CreateTestPsrData(false,
            [4, 3],PilotingSkillRollType.PilotDamageFromFall);

        var sut = CreateBasicFallingCommand() with
        {
            FallPilotingSkillRoll = failedFallPsr,
            PilotDamagePilotingSkillRoll = failedPilotPsr,
            DamageData = CreateTestFallingDamageData(12)
        };

        var failedPsrDetailsRenderedText = failedFallPsr.Render(_localizationService);
        var pilotDamagePsrRenderedText = failedPilotPsr.Render(_localizationService);

        // Act
        var result = sut.Render(_localizationService, _game);

        // Assert
        result.ShouldContain(failedPsrDetailsRenderedText);
        result.ShouldContain(pilotDamagePsrRenderedText);
        result.ShouldContain("Locust LCT-1V fell and took 12 damage");
    }
}
