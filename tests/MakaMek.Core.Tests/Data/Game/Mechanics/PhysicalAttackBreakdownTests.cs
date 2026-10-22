using Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Attack;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Physical;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Terrains;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Data.Game.Mechanics;

public class PhysicalAttackBreakdownTests
{
    private static PhysicalAttackBreakdown CreateBreakdown(
        IReadOnlyList<TerrainRollModifier>? terrainModifiers = null,
        IReadOnlyList<RollModifier>? otherModifiers = null)
    {
        return new PhysicalAttackBreakdown
        {
            PilotingBase = new PilotingRollModifier { Value = 4 },
            AttackTypeModifier = new PhysicalAttackBaseModifier
            {
                Value = -2,
                AttackType = PhysicalAttackType.Kick
            },
            AttackerMovement = new AttackerMovementModifier
            {
                Value = 1,
                MovementType = MovementType.Walk
            },
            TargetMovement = new TargetMovementModifier
            {
                Value = 2,
                HexesMoved = 5
            },
            TerrainModifiers = terrainModifiers ?? [],
            OtherModifiers = otherModifiers ?? []
        };
    }

    [Fact]
    public void AllModifiers_HasFixedOrder_WithBaseFirst()
    {
        // Arrange
        var terrainModifier = new TerrainRollModifier
        {
            Value = 2,
            Location = new HexCoordinates(1, 2),
            TerrainId = MakaMekTerrains.HeavyWoods
        };
        var otherModifier = new HeatRollModifier { Value = 1, HeatLevel = 15 };
        var sut = CreateBreakdown([terrainModifier], [otherModifier]);

        // Act
        var allModifiers = sut.AllModifiers;

        // Assert — base first, then movement modifiers, other modifiers and terrain,
        // each modifier included exactly once (the attack-type base is not duplicated)
        allModifiers.Count.ShouldBe(6);
        allModifiers[0].ShouldBeOfType<PilotingRollModifier>();
        allModifiers[1].ShouldBeOfType<PhysicalAttackBaseModifier>();
        allModifiers[2].ShouldBeOfType<AttackerMovementModifier>();
        allModifiers[3].ShouldBeOfType<TargetMovementModifier>();
        allModifiers[4].ShouldBe(otherModifier);
        allModifiers[5].ShouldBe(terrainModifier);
        allModifiers.Count(m => m is PhysicalAttackBaseModifier).ShouldBe(1);
    }

    [Fact]
    public void Total_IsSumOfAllModifiers()
    {
        // Arrange
        var terrainModifier = new TerrainRollModifier
        {
            Value = 2,
            Location = new HexCoordinates(1, 2),
            TerrainId = MakaMekTerrains.LightWoods
        };
        var sut = CreateBreakdown([terrainModifier]);

        // Act
        var result = sut.Total;

        // Assert — piloting (4) + attack type (-2) + attacker movement (1) + target movement (2) + terrain (2)
        result.ShouldBe(7);
    }

    [Fact]
    public void AllModifiers_IncludesTargetSideModifiers_Once()
    {
        // Arrange
        var partialCover = new PartialCoverModifier { Value = 1 };
        var skiddingTarget = new SkiddingTargetModifier { Value = 2 };
        var sut = CreateBreakdown([], [partialCover, skiddingTarget]);

        // Act
        var allModifiers = sut.AllModifiers;

        // Assert — target-side modifiers sit between the movement modifiers and the terrain ones
        allModifiers.Count.ShouldBe(6);
        allModifiers[4].ShouldBe(partialCover);
        allModifiers[5].ShouldBe(skiddingTarget);
        sut.Total.ShouldBe(8); // piloting (4) + attack type (-2) + attacker movement (1) + target movement (2) + cover (1) + skidding (2) - terrain (0)
    }
}
