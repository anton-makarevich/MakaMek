using NSubstitute;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers;
using Sanet.MakaMek.Core.Services.Localization;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Data.Game.Mechanics;

public class PilotingSkillRollDataTests
{
    private readonly ILocalizationService _localizationService = new FakeLocalizationService();
    
    private PilotingSkillRollData CreateSuccessfulCommand()
    {
        return new PilotingSkillRollData
        {
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
    
    private PilotingSkillRollData CreateFailedCommand()
    {
        return new PilotingSkillRollData
        {
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
    
    private PilotingSkillRollData CreateImpossibleCommand()
    {
        return new PilotingSkillRollData
        {
            RollType = PilotingSkillRollType.GyroHit,
            DiceResults = [], // doesn't matter for impossible rolls
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
    public void Render_ShouldFormatSuccessfulRoll_Correctly()
    {
        // Arrange
        var sut = CreateSuccessfulCommand();
        
        // Act
        var result = sut.Render(_localizationService);
        
        // Assert
        result.ShouldContain("Gyro Hit roll succeeded");
        result.ShouldContain("Base Piloting Skill: 4");
        result.ShouldContain("Modifiers:");
        result.ShouldContain("Damaged Gyro: +2");
        result.ShouldContain("Total Target Number: 6");
        result.ShouldContain("Roll Result: 7");
    }
    
    [Fact]
    public void Render_ShouldFormatFailedRoll_Correctly()
    {
        // Arrange
        var sut = CreateFailedCommand();
        
        // Act
        var result = sut.Render(_localizationService);
        
        // Assert
        result.ShouldContain("Gyro Hit roll failed");
        result.ShouldContain("Base Piloting Skill: 4");
        result.ShouldContain("Modifiers:");
        result.ShouldContain("Damaged Gyro: +2");
        result.ShouldContain("Total Target Number: 6");
        result.ShouldContain("Roll Result: 4");
    }
    
    [Fact]
    public void Render_ShouldFormatImpossibleRoll_Correctly()
    {
        // Arrange
        var sut = CreateImpossibleCommand();
        
        // Act
        var result = sut.Render(_localizationService);
        
        // Assert
        result.ShouldContain("Gyro Hit roll is impossible");
        result.ShouldContain("Base Piloting Skill: 4");
        result.ShouldContain("Modifiers:");
        result.ShouldContain("Damaged Gyro: +9");
        result.ShouldContain("Total Target Number: 13");
        result.ShouldNotContain("Roll Result:"); // No roll result for impossible rolls
    }
    
    // Test helper class for RollModifier
    private record TestModifier : RollModifier
    {
        public required string Name { get; init; }
        
        public override string Render(ILocalizationService localizationService)
        {
            return $"{Name}: +{Value}";
        }
    }
}
