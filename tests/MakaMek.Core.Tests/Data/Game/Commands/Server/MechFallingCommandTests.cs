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
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Core.Utils.TechRules;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Data.Game.Commands.Server;

public class MechFallingCommandTests
{
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();
    private readonly IGame _game = Substitute.For<IGame>();
    private readonly Guid _gameId = Guid.NewGuid();
    private readonly Unit _unit;
    private const string PsrDetailsRenderedText = "PSR Details: Target 7, Rolled 8 (Success)";
    private const string FailedPsrDetailsRenderedText = "PSR Details: Target 7, Rolled 6 (Failure)";
    private const string PilotDamagePsrRenderedText = "Pilot PSR: Target 5, Rolled 4 (Failure)";

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

    private PilotingSkillRollData CreateTestPsrData(bool successful, PilotingSkillRollType rollType = PilotingSkillRollType.GyroHit, string renderedText = PsrDetailsRenderedText)
    {
        var psrData = Substitute.For<PilotingSkillRollData>();
        psrData.IsSuccessful.Returns(successful);
        psrData.RollType.Returns(rollType);
        psrData.Render(Arg.Any<ILocalizationService>()).Returns(renderedText);
        return psrData;
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

    private MechFallingCommand CreateBasicFallingCommand()
    {
        return new MechFallingCommand
        {
            GameOriginId = _gameId,
            UnitId = _unit.Id,
            LevelsFallen = 0,
            WasJumping = false,
            DamageData = CreateTestFallingDamageData(),
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
            new DiceResult(1));

        return new MechFallingCommand
        {
            GameOriginId = _gameId,
            UnitId = _unit.Id,
            LevelsFallen = 0,
            WasJumping = false,
            DamageData = fallingDamageData,
            PilotDamagePilotingSkillRoll = new PilotingSkillRollData
            {
                RollType = PilotingSkillRollType.PilotDamageFromFall,
                DiceResults = [],
                IsSuccessful = false,
                PsrBreakdown = new PsrBreakdown
                {
                    BasePilotingSkill = 1,
                    Modifiers = []
                }
            },
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
            new DiceResult(1));

        return new MechFallingCommand
        {
            GameOriginId = _gameId,
            UnitId = _unit.Id,
            LevelsFallen = 2,
            WasJumping = true,
            DamageData = fallingDamageData,
            PilotDamagePilotingSkillRoll = new PilotingSkillRollData
            {
                RollType = PilotingSkillRollType.PilotDamageFromFall,
                DiceResults = [],
                IsSuccessful = false,
                PsrBreakdown = new PsrBreakdown
                {
                    BasePilotingSkill = 1,
                    Modifiers = []
                }
            },
            Timestamp = DateTime.UtcNow
        };
    }

    [Fact]
    public void Render_ShouldFormatBasicFall_Correctly()
    {
        // Arrange
        var command = CreateBasicFallingCommand();

        // Act
        var result = command.Render(_localizationService, _game);

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
        var command = CreateFallingWithLevelsCommand();

        // Act
        var result = command.Render(_localizationService, _game);

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
        var command = CreateJumpingFallCommand();

        // Act
        var result = command.Render(_localizationService, _game);

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
        var command = CreatePilotInjuryCommand();

        // Act
        var result = command.Render(_localizationService, _game);

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
        var command = CreateComplexFallCommand();

        // Act
        var result = command.Render(_localizationService, _game);

        // Assert
        result.ShouldNotBeEmpty();
        result.ShouldContain("Locust LCT-1V fell");
        result.ShouldContain("2 level(s)");
        result.ShouldContain("while jumping");
        result.ShouldContain("and took 8 damage");
        result.ShouldContain("pilot was injured");
    }

    [Fact]
    public void Render_ShouldReturnEmpty_WhenUnitNotFound()
    {
        // Arrange
        var command = CreateBasicFallingCommand() with { UnitId = Guid.NewGuid() };

        // Act
        var result = command.Render(_localizationService, _game);

        // Assert
        result.ShouldBeEmpty();
    }
    
    [Fact]
    public void Render_ShouldIncludePilotingSkillRollData_WhenProvided()
    {
        // Arrange
        var command = CreateBasicFallingCommand();
        
        // Setup localization service for PSR rendering
        _localizationService.GetString("PilotingSkillRollType_GyroHit").Returns("Gyro Hit");
        _localizationService.GetString("Command_PilotingSkillRoll_Success").Returns("PSR for {0} succeeded");
        _localizationService.GetString("Command_PilotingSkillRoll_BasePilotingSkill").Returns("Base Piloting Skill: {0}");
        _localizationService.GetString("Command_PilotingSkillRoll_TotalTargetNumber").Returns("Target Number: {0}");
        _localizationService.GetString("Command_PilotingSkillRoll_RollResult").Returns("Roll Result: {0}");
        
        // Create PSR data
        var psrBreakdown = new PsrBreakdown
        {
            BasePilotingSkill = 4,
            Modifiers = []
        };
        
        var fallPsr = new PilotingSkillRollData
        {
            RollType = PilotingSkillRollType.GyroHit,
            DiceResults = [4, 3],
            IsSuccessful = true,
            PsrBreakdown = psrBreakdown
        };
        
        command = command with 
        { 
            FallPilotingSkillRoll = fallPsr
        };

        // Act
        var result = command.Render(_localizationService, _game);

        // Assert
        result.ShouldNotBeEmpty();
        result.ShouldContain("PSR for Gyro Hit succeeded");
        result.ShouldContain("Base Piloting Skill: 4");
        result.ShouldContain("Locust LCT-1V fell");
    }
    
    [Fact]
    public void Render_ShouldIncludePilotDamagePsr_WhenProvided()
    {
        // Arrange
        var command = CreatePilotInjuryCommand();
        
        // Setup localization service for PSR rendering
        _localizationService.GetString("PilotingSkillRollType_PilotDamageFromFall").Returns("Pilot Damage");
        _localizationService.GetString("Command_PilotingSkillRoll_Failure").Returns("PSR for {0} failed");
        _localizationService.GetString("Command_PilotingSkillRoll_BasePilotingSkill").Returns("Base Piloting Skill: {0}");
        _localizationService.GetString("Command_PilotingSkillRoll_TotalTargetNumber").Returns("Target Number: {0}");
        _localizationService.GetString("Command_PilotingSkillRoll_RollResult").Returns("Roll Result: {0}");
        
        // Create PSR data
        var psrBreakdown = new PsrBreakdown
        {
            BasePilotingSkill = 4,
            Modifiers = []
        };
        
        var pilotDamagePsr = new PilotingSkillRollData
        {
            RollType = PilotingSkillRollType.PilotDamageFromFall,
            DiceResults = [2, 3],
            IsSuccessful = false,
            PsrBreakdown = psrBreakdown
        };
        
        command = command with 
        { 
            PilotDamagePilotingSkillRoll = pilotDamagePsr
        };

        // Act
        var result = command.Render(_localizationService, _game);

        // Assert
        result.ShouldNotBeEmpty();
        result.ShouldContain("Locust LCT-1V fell");
        result.ShouldContain("pilot was injured");
        result.ShouldContain("PSR for Pilot Damage failed");
    }

    [Fact]
    public void Render_AutoFall_GyroDestroyed_ShouldRenderFallDetails_WithoutFallPsrInfo()
    {
        // Arrange
        var command = CreateBasicFallingCommand() with
        {
            FallPilotingSkillRoll = null, // Auto-fall, no PSR for the fall itself
            DamageData = CreateTestFallingDamageData(10) // Ensure damage is present
        };

        // Act
        var result = command.Render(_localizationService, _game);

        // Assert
        result.ShouldBe($"Locust LCT-1V fell and took 10 damage");
        result.ShouldNotContain(PsrDetailsRenderedText);
        result.ShouldNotContain(FailedPsrDetailsRenderedText);
    }

    [Fact]
    public void Render_SuccessfulPsr_GyroHit_ShouldRenderPsrSuccessMessage_AndPsrDetails()
    {
        // Arrange
        var successfulPsr = CreateTestPsrData(true);
        var command = new MechFallingCommand
        {
            GameOriginId = _gameId,
            UnitId = _unit.Id,
            DamageData = null, // No fall damage
            FallPilotingSkillRoll = successfulPsr,
            Timestamp = DateTime.UtcNow
        };

        // Act
        var result = command.Render(_localizationService, _game);

        // Assert
        result.ShouldBe($"{PsrDetailsRenderedText}{Environment.NewLine}Locust LCT-1V successfully compensated for a gyro hit.");
    }
    
    [Fact]
    public void Render_SuccessfulPsr_LowerLegActuatorHit_ShouldRenderPsrSuccessMessage_AndPsrDetails()
    {
        // Arrange
        var successfulPsr = CreateTestPsrData(true, PilotingSkillRollType.LowerLegActuatorHit);
        var command = new MechFallingCommand
        {
            GameOriginId = _gameId,
            UnitId = _unit.Id,
            DamageData = null, // No fall damage
            FallPilotingSkillRoll = successfulPsr,
            Timestamp = DateTime.UtcNow
        };

        // Act
        var result = command.Render(_localizationService, _game);

        // Assert
        result.ShouldBe($"{PsrDetailsRenderedText}{Environment.NewLine}Locust LCT-1V successfully compensated for a lower leg actuator hit.");
    }

    [Fact]
    public void Render_FailedPsr_ResultsInFall_ShouldRenderPsrDetails_AndFallDetails()
    {
        // Arrange
        var failedPsr = CreateTestPsrData(false, PilotingSkillRollType.GyroHit, FailedPsrDetailsRenderedText);
        var command = CreateBasicFallingCommand() with
        {
            FallPilotingSkillRoll = failedPsr,
            DamageData = CreateTestFallingDamageData(7) // Fall damage occurred
        };

        // Act
        var result = command.Render(_localizationService, _game);

        // Assert
        result.ShouldBe($"{FailedPsrDetailsRenderedText}{Environment.NewLine}Locust LCT-1V fell and took 7 damage");
    }

    [Fact]
    public void Render_NoDamage_NoFallPsr_ShouldRenderMinimally()
    {
        // Arrange
        var command = new MechFallingCommand
        {
            GameOriginId = _gameId,
            UnitId = _unit.Id,
            DamageData = null,
            FallPilotingSkillRoll = null,
            Timestamp = DateTime.UtcNow
        };

        // Act
        var result = command.Render(_localizationService, _game);

        // Assert
        result.ShouldBeEmpty(); // Expecting empty if no PSR and no damage
    }

    [Fact]
    public void Render_FallPsrDetails_PilotDamagePsrDetails_AndFallDamage_Correctly()
    {
        // Arrange
        var failedFallPsr = CreateTestPsrData(false, PilotingSkillRollType.GyroHit, FailedPsrDetailsRenderedText);
        var failedPilotPsr = CreateTestPsrData(false, PilotingSkillRollType.PilotDamageFromFall, PilotDamagePsrRenderedText);

        var command = CreateBasicFallingCommand() with
        {
            FallPilotingSkillRoll = failedFallPsr,
            PilotDamagePilotingSkillRoll = failedPilotPsr,
            DamageData = CreateTestFallingDamageData(12)
        };

        // Act
        var result = command.Render(_localizationService, _game);

        // Assert
        result.ShouldBe($"{FailedPsrDetailsRenderedText}{Environment.NewLine}Locust LCT-1V fell and took 12 damage, pilot was injured{PilotDamagePsrRenderedText}");
    }
}
