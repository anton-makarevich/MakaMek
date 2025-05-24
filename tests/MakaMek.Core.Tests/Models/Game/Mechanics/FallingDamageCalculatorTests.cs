using Moq;
using NSubstitute;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Core.Models.Units.Pilots;
using Sanet.MakaMek.Core.Utils.TechRules;
using Shouldly;
using System;
using System.Collections.Generic;
using Xunit;

namespace MakaMek.Core.Tests.Models.Game.Mechanics;

public class FallingDamageCalculatorTests
{
    private readonly FallingDamageCalculator _calculator;
    private readonly Mech _mech;
    
    public FallingDamageCalculatorTests()
    {
        // Create a test mech
        _mech = new Mech(
            "Test", 
            "Mech", 
            50, // 50 ton mech
            5,  // 5 movement points
            new List<UnitPart>(),
            1,
            Guid.NewGuid());
        
        // Set up a test crew
        _mech.Crew = new MechWarrior("Test", "Pilot");
        
        // Create the calculator
        _calculator = new FallingDamageCalculator();
    }
    
    [Fact]
    public void CalculateFallingDamage_ShouldCalculateCorrectDamage_ForNonJumpingMech()
    {
        // Arrange
        int levelsFallen = 2;
        bool wasJumping = false;
        
        // Act
        var result = _calculator.CalculateFallingDamage(_mech, levelsFallen, wasJumping);
        
        // Assert
        // For a 50-ton mech: 5 damage per group (ceiling of 50/10)
        // Total damage = 5 * (2 + 1) = 15
        result.DamagePerGroup.ShouldBe(5);
        result.TotalDamage.ShouldBe(15);
        result.FacingDiceRoll.ShouldNotBeNull();
        result.HitLocation.ShouldNotBe(default);
    }
    
    [Fact]
    public void CalculateFallingDamage_ShouldCalculateCorrectDamage_ForJumpingMech()
    {
        // Arrange
        int levelsFallen = 2;
        bool wasJumping = true;
        
        // Act
        var result = _calculator.CalculateFallingDamage(_mech, levelsFallen, wasJumping);
        
        // Assert
        // For a 50-ton mech: 5 damage per group (ceiling of 50/10)
        // When jumping, levelsFallen is treated as 0
        // Total damage = 5 * (0 + 1) = 5
        result.DamagePerGroup.ShouldBe(5);
        result.TotalDamage.ShouldBe(5);
    }
    
    [Fact]
    public void DetermineWarriorDamage_ShouldReturnTrue_WhenMechIsImmobile()
    {
        // Arrange
        _mech.Status |= UnitStatus.Immobile;
        var psrBreakdown = new PsrBreakdown
        {
            BasePilotingSkill = 4,
            Modifiers = new List<Modifiers.RollModifier>()
        };
        var diceRoll = new DiceRoll(2, 6) { Result = 7 }; // Would normally pass
        
        // Act
        var result = _calculator.DetermineWarriorDamage(_mech, 1, psrBreakdown, diceRoll);
        
        // Assert
        result.ShouldBeTrue();
    }
    
    [Fact]
    public void DetermineWarriorDamage_ShouldReturnTrue_WhenPilotIsUnconscious()
    {
        // Arrange
        _mech.Status = UnitStatus.Active;
        _mech.Crew.IsUnconscious = true;
        var psrBreakdown = new PsrBreakdown
        {
            BasePilotingSkill = 4,
            Modifiers = new List<Modifiers.RollModifier>()
        };
        var diceRoll = new DiceRoll(2, 6) { Result = 7 }; // Would normally pass
        
        // Act
        var result = _calculator.DetermineWarriorDamage(_mech, 1, psrBreakdown, diceRoll);
        
        // Assert
        result.ShouldBeTrue();
    }
    
    [Fact]
    public void DetermineWarriorDamage_ShouldReturnTrue_WhenPsrIsImpossible()
    {
        // Arrange
        _mech.Status = UnitStatus.Active;
        var psrBreakdown = new PsrBreakdown
        {
            BasePilotingSkill = 13, // Impossible roll
            Modifiers = new List<Modifiers.RollModifier>()
        };
        var diceRoll = new DiceRoll(2, 6) { Result = 12 }; // Even max roll would fail
        
        // Act
        var result = _calculator.DetermineWarriorDamage(_mech, 1, psrBreakdown, diceRoll);
        
        // Assert
        result.ShouldBeTrue();
    }
    
    [Theory]
    [InlineData(4, 0, 3, false)] // Roll 3 vs target 4 = pass
    [InlineData(4, 0, 5, true)]  // Roll 5 vs target 4 = fail
    [InlineData(4, 2, 5, false)] // Roll 5 vs target 6 (4+2) = pass
    [InlineData(4, 2, 7, true)]  // Roll 7 vs target 6 (4+2) = fail
    public void DetermineWarriorDamage_ShouldReturnExpectedResult_BasedOnRoll(
        int basePiloting, int levelModifier, int rollResult, bool expectedResult)
    {
        // Arrange
        _mech.Status = UnitStatus.Active;
        _mech.Crew.Piloting = basePiloting;
        var psrBreakdown = new PsrBreakdown
        {
            BasePilotingSkill = basePiloting,
            Modifiers = new List<Modifiers.RollModifier>()
        };
        var diceRoll = new DiceRoll(2, 6) { Result = rollResult };
        
        // Act
        var result = _calculator.DetermineWarriorDamage(_mech, levelModifier, psrBreakdown, diceRoll);
        
        // Assert
        result.ShouldBe(expectedResult);
    }
}
