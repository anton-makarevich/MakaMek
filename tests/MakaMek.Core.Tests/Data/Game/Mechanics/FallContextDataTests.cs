using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Mechs.Falling;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Models;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Data.Game.Mechanics;

public class FallContextDataTests
{
    private readonly Guid _unitId = Guid.NewGuid();
    private readonly Guid _gameId = Guid.NewGuid();
    
    [Fact]
    public void ToMechFallCommand_ShouldCreateCorrectCommand()
    {
        // Arrange
        var fallingDamageData = new FallingDamageData(
            HexDirection.Top,
            new HitLocationsData([], 10),
            new DiceResult(6),
            HitDirection.Front
        );
        
        var pilotingSkillRoll = new PilotingSkillRollData
        {
            RollType = PilotingSkillRollType.GyroHit,
            DiceResults = [3, 4], // Total 7
            IsSuccessful = false,
            PsrBreakdown = new PsrBreakdown
            {
                BasePilotingSkill = 4,
                Modifiers = []
            }
        };
        
        var pilotDamageRoll = new PilotingSkillRollData
        {
            RollType = PilotingSkillRollType.PilotDamageFromFall,
            DiceResults = [5, 6], // Total 11
            IsSuccessful = true,
            PsrBreakdown = new PsrBreakdown
            {
                BasePilotingSkill = 5,
                Modifiers = []
            }
        };
        
        var fallContextData = new FallContextData
        {
            UnitId = _unitId,
            GameId = _gameId,
            IsFalling = true,
            ReasonType = FallReasonType.GyroHit,
            PilotingSkillRoll = pilotingSkillRoll,
            PilotDamagePilotingSkillRoll = pilotDamageRoll,
            FallingDamageData = fallingDamageData,
            LevelsFallen = 2,
            WasJumping = true
        };
        
        // Act
        var result = fallContextData.ToMechFallCommand();
        
        // Assert
        result.UnitId.ShouldBe(_unitId);
        result.GameOriginId.ShouldBe(_gameId);
        result.LevelsFallen.ShouldBe(2);
        result.WasJumping.ShouldBeTrue();
        result.DamageData.ShouldBe(fallingDamageData);
        result.FallPilotingSkillRoll.ShouldBe(pilotingSkillRoll);
        result.PilotDamagePilotingSkillRoll.ShouldBe(pilotDamageRoll);
        result.IsPilotingSkillRollRequired.ShouldBeTrue();
        result.IsPilotTakingDamage.ShouldBeFalse();
    }
    
    [Fact]
    public void ToMechStandUpCommand_WhenNotFallingAndHasRoll_ShouldCreateCorrectCommand()
    {
        // Arrange
        var pilotingSkillRoll = new PilotingSkillRollData
        {
            RollType = PilotingSkillRollType.StandupAttempt,
            DiceResults = [4, 5], // Total 9
            IsSuccessful = true,
            PsrBreakdown = new PsrBreakdown
            {
                BasePilotingSkill = 4,
                Modifiers = []
            }
        };
        
        var fallContextData = new FallContextData
        {
            UnitId = _unitId,
            GameId = _gameId,
            IsFalling = false, // Not falling, attempting to stand up
            ReasonType = FallReasonType.StandUpAttempt,
            PilotingSkillRoll = pilotingSkillRoll
        };
        
        // Act
        var result = fallContextData.ToMechStandUpCommand(HexDirection.Bottom);
        
        // Assert
        result.ShouldNotBeNull();
        result.Value.UnitId.ShouldBe(_unitId);
        result.Value.GameOriginId.ShouldBe(_gameId);
        result.Value.PilotingSkillRoll.ShouldBe(pilotingSkillRoll);
        result.Value.NewFacing.ShouldBe(HexDirection.Bottom);
    }
    
    [Fact]
    public void ToMechStandUpCommand_WhenFalling_ShouldReturnNull()
    {
        // Arrange
        var pilotingSkillRoll = new PilotingSkillRollData
        {
            RollType = PilotingSkillRollType.StandupAttempt,
            DiceResults = [4, 5],
            IsSuccessful = true,
            PsrBreakdown = new PsrBreakdown
            {
                BasePilotingSkill = 4,
                Modifiers = []
            }
        };
        
        var fallContextData = new FallContextData
        {
            UnitId = _unitId,
            GameId = _gameId,
            IsFalling = true, // Unit is falling, not standing up
            ReasonType = FallReasonType.GyroHit,
            PilotingSkillRoll = pilotingSkillRoll
        };
        
        // Act
        var result = fallContextData.ToMechStandUpCommand(HexDirection.Bottom);
        
        // Assert
        result.ShouldBeNull();
    }
    
    [Fact]
    public void ToMechStandUpCommand_WhenNoPilotingSkillRoll_ShouldReturnNull()
    {
        // Arrange
        var fallContextData = new FallContextData
        {
            UnitId = _unitId,
            GameId = _gameId,
            IsFalling = false,
            ReasonType = FallReasonType.StandUpAttempt,
            PilotingSkillRoll = null // No piloting skill roll data
        };
        
        // Act
        var result = fallContextData.ToMechStandUpCommand(HexDirection.Bottom);
        
        // Assert
        result.ShouldBeNull();
    }
}
