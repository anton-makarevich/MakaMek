using NSubstitute;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.Tests.Models.Game.Mechanics.Mechs.Falling;
using Sanet.MakaMek.Core.Tests.Utils;
using Sanet.MakaMek.Core.Utils;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Data.Game.Commands.Server;

public class MechFallCommandTests
{
    private readonly ILocalizationService _localizationService = new FakeLocalizationService();
    private readonly IGame _game = Substitute.For<IGame>();
    private readonly Guid _gameId = Guid.NewGuid();
    private readonly Unit _unit;
    private const string PsrDetailsText = "Base Piloting Skill";

    private static PilotingSkillRollData CreateTestPsrData(
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
    
    private static LocationHitData CreateHitDataForLocation(PartLocation partLocation,
        int damage,
        int[]? aimedShotRoll = null,
        int[]? locationRoll = null)
    {
        return new LocationHitData(
        [
            new LocationDamageData(partLocation,
                damage-1,
                1,
                false)
        ], aimedShotRoll??[], locationRoll??[], partLocation);
    }

    public MechFallCommandTests()
    {
        var player =
            // Create player
            new Player(Guid.NewGuid(), "Player 1", PlayerControlType.Human);

        // Create a unit using MechFactory
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

    private FallingDamageData CreateTestFallingDamageData(int totalDamage = 5)
    {
        var hitLocations = new List<LocationHitData>
        {
            CreateHitDataForLocation(PartLocation.CenterTorso, totalDamage, [],[6])
        };
        var hitLocationsData = new HitLocationsData(hitLocations, totalDamage);
        return new FallingDamageData(HexDirection.Top, hitLocationsData, new DiceResult(1), HitDirection.Front);
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
        var hitLocations = new List<LocationHitData>
        {
            CreateHitDataForLocation(PartLocation.CenterTorso, 5, [],[6])
        };
        
        var hitLocationsData = new HitLocationsData(
            hitLocations,
            5);
        
        var fallingDamageData = new FallingDamageData(
            HexDirection.Top,
            hitLocationsData,
            new DiceResult(1), HitDirection.Front);

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
        var hitLocations = new List<LocationHitData>
        {
            CreateHitDataForLocation(PartLocation.CenterTorso, 5, [],[6]),
            CreateHitDataForLocation(PartLocation.LeftLeg, 3, [],[4])
        };
        
        var hitLocationsData = new HitLocationsData(
            hitLocations,
            8);
        
        var fallingDamageData = new FallingDamageData(
            HexDirection.Top,
            hitLocationsData,
            new DiceResult(1), HitDirection.Front);

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
        result.ShouldContain("LCT-1V fell");
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
        result.ShouldContain("LCT-1V fell");
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
        result.ShouldContain("LCT-1V fell");
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
        result.ShouldContain("LCT-1V fell");
        result.ShouldContain("and took 5 damage");
        result.ShouldContain("pilot was injured");
    }

    [Fact]
    public void Render_ShouldIncludeAllElements_ForComplexFall()
    {
        // Arrange
        var sut = CreateComplexFallCommand();

        // Act
        var result = sut.Render(_localizationService, _game);

        // Assert
        result.ShouldNotBeEmpty();
        result.ShouldContain("LCT-1V fell");
        result.ShouldContain("2 level(s)");
        result.ShouldContain("while jumping");
        result.ShouldContain("and took 8 damage");
        result.ShouldContain("Hit Locations:");
        foreach (var hitLocation in sut.DamageData!.HitLocations.HitLocations)
        {
            var text = hitLocation.Render(_localizationService).Trim();
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
        result.ShouldContain("LCT-1V fell");
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
        result.ShouldContain($"LCT-1V fell and took 10 damage");
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
        result.ShouldContain("LCT-1V may fall");
        result.ShouldContain(psrDetailsRenderedText);
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
        result.ShouldContain("LCT-1V fell and took 7 damage");
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
        result.ShouldContain("LCT-1V fell and took 12 damage");
    }
}
