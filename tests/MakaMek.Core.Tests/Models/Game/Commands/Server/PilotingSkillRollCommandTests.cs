using Shouldly;
using NSubstitute;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Commands.Server;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.Utils.TechRules;
using Sanet.MakaMek.Core.Tests.Data.Community;
using Sanet.MakaMek.Core.Utils;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Commands.Server;

public class PilotingSkillRollCommandTests
{
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();
    private readonly IGame _game = Substitute.For<IGame>();
    private readonly Guid _gameId = Guid.NewGuid();
    private readonly Unit _unit;

    public PilotingSkillRollCommandTests()
    {
        var player =
            // Create player
            new Player(Guid.NewGuid(), "Test Player");

        // Create unit using MechFactory
        var mechFactory = new MechFactory(new ClassicBattletechRulesProvider(), _localizationService);
        var unitData = MechFactoryTests.CreateDummyMechData();
        unitData.Id = Guid.NewGuid();
        _unit = mechFactory.Create(unitData);
        
        // Add unit to player
        player.AddUnit(_unit);
        
        // Setup game to return players
        _game.Players.Returns(new List<Player> { player });
        
        // Setup localization service
        SetupLocalizationService();
    }
    
    private void SetupLocalizationService()
    {
        _localizationService.GetString("PilotingSkillRollType_GyroHit").Returns("Gyro Hit");
        _localizationService.GetString("Command_PilotingSkillRoll_Success").Returns("{0}'s {1} succeeds {2} check");
        _localizationService.GetString("Command_PilotingSkillRoll_Failure").Returns("{0}'s {1} fails {2} check");
        _localizationService.GetString("Command_PilotingSkillRoll_ImpossibleRoll").Returns("{0}'s {1} automatically fails {2} check (impossible roll)");
        _localizationService.GetString("Command_PilotingSkillRoll_BasePilotingSkill").Returns("Base Piloting Skill: {0}");
        _localizationService.GetString("Command_PilotingSkillRoll_Modifiers").Returns("Modifiers:");
        _localizationService.GetString("Command_PilotingSkillRoll_Modifier").Returns("  - {0}: +{1}");
        _localizationService.GetString("Command_PilotingSkillRoll_TotalTargetNumber").Returns("Total Target Number: {0}");
        _localizationService.GetString("Command_PilotingSkillRoll_RollResult").Returns("Roll Result: {0}");
    }
    
    private PilotingSkillRollCommand CreateSuccessfulCommand()
    {
        return new PilotingSkillRollCommand
        {
            GameOriginId = _gameId,
            UnitId = _unit.Id,
            RollType = PilotingSkillRollType.GyroHit,
            DiceResults = [3, 4], // Total 7
            IsSuccessful = true,
            PsrBreakdown = new PsrBreakdown
            {
                BasePilotingSkill = 4,
                Modifiers = new List<RollModifier> 
                { 
                    new TestModifier { Value = 2, Name = "Damaged Gyro" }
                }
            }
        };
    }
    
    private PilotingSkillRollCommand CreateFailedCommand()
    {
        return new PilotingSkillRollCommand
        {
            GameOriginId = _gameId,
            UnitId = _unit.Id,
            RollType = PilotingSkillRollType.GyroHit,
            DiceResults = [2, 2], // Total 4
            IsSuccessful = false,
            PsrBreakdown = new PsrBreakdown
            {
                BasePilotingSkill = 4,
                Modifiers = new List<RollModifier> 
                { 
                    new TestModifier { Value = 2, Name = "Damaged Gyro" }
                }
            }
        };
    }
    
    private PilotingSkillRollCommand CreateImpossibleCommand()
    {
        return new PilotingSkillRollCommand
        {
            GameOriginId = _gameId,
            UnitId = _unit.Id,
            RollType = PilotingSkillRollType.GyroHit,
            DiceResults = [6, 6], // Total 12, but doesn't matter for impossible rolls
            IsSuccessful = false,
            PsrBreakdown = new PsrBreakdown
            {
                BasePilotingSkill = 4,
                Modifiers = new List<RollModifier> 
                { 
                    new TestModifier { Value = 9, Name = "Damaged Gyro" } // Makes total 13, which is impossible
                }
            }
        };
    }
    
    [Fact]
    public void Format_ShouldFormatSuccessfulRoll_Correctly()
    {
        // Arrange
        var command = CreateSuccessfulCommand();
        
        // Act
        var result = command.Format(_localizationService, _game);
        
        // Assert
        result.ShouldContain("Test Player's Locust LCT-1V succeeds Gyro Hit check");
        result.ShouldContain("Base Piloting Skill: 4");
        result.ShouldContain("Modifiers:");
        result.ShouldContain("  - Damaged Gyro: +2");
        result.ShouldContain("Total Target Number: 6");
        result.ShouldContain("Roll Result: 7");
    }
    
    [Fact]
    public void Format_ShouldFormatFailedRoll_Correctly()
    {
        // Arrange
        var command = CreateFailedCommand();
        
        // Act
        var result = command.Format(_localizationService, _game);
        
        // Assert
        result.ShouldContain("Test Player's Locust LCT-1V fails Gyro Hit check");
        result.ShouldContain("Base Piloting Skill: 4");
        result.ShouldContain("Modifiers:");
        result.ShouldContain("  - Damaged Gyro: +2");
        result.ShouldContain("Total Target Number: 6");
        result.ShouldContain("Roll Result: 4");
    }
    
    [Fact]
    public void Format_ShouldFormatImpossibleRoll_Correctly()
    {
        // Arrange
        var command = CreateImpossibleCommand();
        
        // Act
        var result = command.Format(_localizationService, _game);
        
        // Assert
        result.ShouldContain("Test Player's Locust LCT-1V automatically fails Gyro Hit check (impossible roll)");
        result.ShouldContain("Base Piloting Skill: 4");
        result.ShouldContain("Modifiers:");
        result.ShouldContain("  - Damaged Gyro: +9");
        result.ShouldContain("Total Target Number: 13");
        result.ShouldNotContain("Roll Result:"); // No roll result for impossible rolls
    }
    
    [Fact]
    public void Format_ShouldReturnEmpty_WhenUnitNotFound()
    {
        // Arrange
        var command = CreateSuccessfulCommand();
        command = command with { UnitId = Guid.NewGuid() }; // Set to a non-existent unit ID
        
        // Act
        var result = command.Format(_localizationService, _game);
        
        // Assert
        result.ShouldBeEmpty();
    }
    
    // Test helper class for RollModifier
    private record TestModifier : RollModifier
    {
        public required string Name { get; init; }
        
        public override string Format(ILocalizationService localizationService)
        {
            return Name;
        }
    }
}
