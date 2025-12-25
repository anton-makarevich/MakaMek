using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Attack;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Data.Game.Mechanics;

public class ToHitBreakdownTest
{
    [Fact]
    public void Total_ReturnsImpossibleRoll_WhenNoLineOfSight()
    {
        // Arrange
        var breakdown = new ToHitBreakdown
        {
            HasLineOfSight = false,
            FiringArc = FiringArc.Front,
            GunneryBase = new GunneryRollModifier { Value = 4 },
            AttackerMovement = new AttackerMovementModifier { Value = 0, MovementType = MovementType.StandingStill },
            TargetMovement = new TargetMovementModifier { Value = 0, HexesMoved = 0 },
            OtherModifiers = [],
            RangeModifier = new RangeRollModifier
                { Value = 0, Range = WeaponRange.Medium, Distance = 5, WeaponName = "Test" },
            TerrainModifiers = []
        };

        // Act & Assert
        breakdown.Total.ShouldBe(ToHitBreakdown.ImpossibleRoll);
    }
    
    [Fact]
    public void Total_ReturnsImpossibleRoll_WhenOutsideFiringArc()
    {
        // Arrange
        var breakdown = new ToHitBreakdown
        {
            HasLineOfSight = true,
            FiringArc = null,
            GunneryBase = new GunneryRollModifier { Value = 4 },
            AttackerMovement = new AttackerMovementModifier { Value = 0, MovementType = MovementType.StandingStill },
            TargetMovement = new TargetMovementModifier { Value = 0, HexesMoved = 0 },
            OtherModifiers = [],
            RangeModifier = new RangeRollModifier
                { Value = 0, Range = WeaponRange.Medium, Distance = 5, WeaponName = "Test" },
            TerrainModifiers = []
        };

        // Act & Assert
        breakdown.Total.ShouldBe(ToHitBreakdown.ImpossibleRoll);
    }
    
    [Fact]
    public void Total_ReturnsCorrectValue_WhenInFiringArcAndHasLineOfSight()
    {
        // Arrange
        var breakdown = new ToHitBreakdown
        {
            HasLineOfSight = true,
            FiringArc = FiringArc.Front,
            GunneryBase = new GunneryRollModifier { Value = 4 },
            AttackerMovement = new AttackerMovementModifier { Value = 1, MovementType = MovementType.StandingStill },
            TargetMovement = new TargetMovementModifier { Value = 2, HexesMoved = 0 },
            OtherModifiers = [],
            RangeModifier = new RangeRollModifier
                { Value = 1, Range = WeaponRange.Medium, Distance = 5, WeaponName = "Test" },
            TerrainModifiers = []
        };

        // Act & Assert
        breakdown.Total.ShouldBe(8); //4+1+2+1
    }
}